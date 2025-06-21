using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Formats.Asn1;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestPlatform.PlatformAbstractions.Interfaces;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SehensWerte.Maths;

namespace SehensWerte.Files
{
    public class AudioReader
    {
        public int ChannelCount { get; private set; }
        public double SamplesPerSecond { get; private set; }
        public int BitDepth { get; private set; } = 16;
        public double[][]? Buffer { get; private set; }
        
        public double[] ChannelSum
        {
            get
            {
                if (ChannelCount == 0 || Buffer == null) return new double[0];
                double[] result = new double[Buffer[0].Length];
                for (int loop=0; loop<ChannelCount; loop++)
                {
                    result = result.Add(Buffer[loop]);
                }
                return result;
            }
        }

        public double[] Channel(int channel) => (Buffer == null || Buffer.Length <= channel) ? new double[0] : Buffer[channel];

        public static double[] ToDouble(string fileName, out double samplesPerSecond)
        {
            AudioReader reader = new AudioReader(fileName);
            samplesPerSecond = reader.SamplesPerSecond;
            return reader.Channel(0);
        }

        public static short[] ToShort(string fileName)
        {
            AudioReader reader = new AudioReader(fileName);
            double[] array = reader.ChannelSum;
            short[] shortArray = new short[array.Length];
            for (int loop = 0; loop < array.Length; loop++)
            {
                int num = (int)(array[loop] * 32767.0);
                shortArray[loop] = (short)Math.Clamp(num, -32768, 32767);
            }
            return shortArray;
        }



        // 2025-01-01-git-d3aa99a4f4-_build-www.gyan.dev
        private const string AV_CODEC_LIB = "avcodec-61.dll";
        private const string AV_FORMAT_LIB = "avformat-61.dll";
        private const string AV_UTIL_LIB = "avutil-59.dll";
        private const string AV_FILTER_LIB = "avfilter-10.dll";


        private enum AVCodecID : int
        {
            FIRST_AUDIO = 0x10000,     ///< A dummy id pointing at the start of audio codecs
            PCM_S16LE = 0x10000,
            MP3 = 0x15001,
            AAC = 0x15002,
            FLAC = 0x1500c,
        }

        private enum AVMediaType : int
        {
            UNKNOWN = -1,  // Usually treated as AVMEDIA_TYPE_DATA
            VIDEO,
            AUDIO,
            DATA,          // Opaque data information usually continuous
            SUBTITLE,
            ATTACHMENT,    // Opaque data information usually sparse
            NB
        };

        // --- P/Invoke Imports ---
        [DllImport(AV_FORMAT_LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern int avformat_open_input(ref IntPtr formatContext, string filename, IntPtr fmt, IntPtr options);

        [DllImport(AV_FORMAT_LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern int avformat_find_stream_info(IntPtr formatContext, IntPtr options);

        [DllImport(AV_FORMAT_LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern int av_read_frame(IntPtr formatContext, IntPtr packet);

        [DllImport(AV_FORMAT_LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern void av_dump_format(IntPtr formatContext, int index, string filename, int is_output);

        [DllImport(AV_FORMAT_LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern int av_find_best_stream(IntPtr formatContext,
                        AVMediaType type,
                        int wanted_stream_nb,
                        int related_stream,
                        IntPtr decoder_ret /* const struct AVCodec** */,
                        int flags);


        [DllImport(AV_FORMAT_LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern void avformat_close_input(ref IntPtr formatContext);

        [DllImport(AV_CODEC_LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr avcodec_find_decoder(int codec_id);

        [DllImport(AV_CODEC_LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr avcodec_alloc_context3(IntPtr codec);

        [DllImport(AV_CODEC_LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern int avcodec_parameters_to_context(IntPtr codecContext, IntPtr codecPar);

        [DllImport(AV_CODEC_LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern int avcodec_open2(IntPtr codecContext, IntPtr codec, IntPtr options);

        [DllImport(AV_CODEC_LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern void avcodec_close(IntPtr codecContext);

        [DllImport(AV_CODEC_LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr av_packet_alloc();

        [DllImport(AV_UTIL_LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr av_frame_alloc();

        [DllImport(AV_CODEC_LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern int avcodec_send_packet(IntPtr codecContext, IntPtr packet);

        [DllImport(AV_CODEC_LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern int avcodec_receive_frame(IntPtr codecContext, IntPtr frame);

        [DllImport(AV_CODEC_LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern void av_packet_unref(IntPtr packet);

        [DllImport(AV_UTIL_LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern void av_frame_free(ref IntPtr frame);

        [DllImport(AV_CODEC_LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern void av_packet_free(ref IntPtr packet);


        [StructLayout(LayoutKind.Sequential)]
        private struct AVRational
        {
            public int num;
            public int den;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AVChannelLayout
        {
            public int order; //  enum AVChannelOrder order;
            public int nb_channels;
            public UInt64 mask;
            public IntPtr opaque; // void*
        }

        private enum AVSampleFormat
        {
            NONE = -1,
            U8,          ///< unsigned 8 bits
            S16,         ///< signed 16 bits
            S32,         ///< signed 32 bits
            FLT,         ///< float
            DBL,         ///< double
            U8P,         ///< unsigned 8 bits, planar
            S16P,        ///< signed 16 bits, planar
            S32P,        ///< signed 32 bits, planar
            FLTP,        ///< float, planar
            DBLP,        ///< double, planar
            S64,         ///< signed 64 bits
            S64P,        ///< signed 64 bits, planar
            NB           ///< Number of sample formats. DO NOT USE if linking dynamically
        };

        [StructLayout(LayoutKind.Sequential)]
        private struct AVCodecContext
        {
            public IntPtr av_class; // const AVClass* 
            public int log_level_offset;
            public int codec_type; // AVMediaType 
            public IntPtr codec; // const struct AVCodec  
            public int codec_id; // AVCodecID     
            public uint codec_tag;
            public IntPtr priv_data; // void*
            public IntPtr internal1; // struct AVCodecInternal *
            public IntPtr opaque;
            public Int64 bit_rate;
            public int flags;
            public int flags2;
            public IntPtr extradata; // uint8_t*
            public int extradata_size;
            public AVRational time_base;
            public AVRational pkt_timebase;
            public AVRational framerate;
            public int delay;
            public int width, height;
            public int coded_width, coded_height;
            public AVRational sample_aspect_ratio;
            public int pix_fmt; // enum AVPixelFormat 
            public int sw_pix_fmt; // enum AVPixelFormat 
            public int color_primaries; // enum AVColorPrimaries 
            public int color_trc; // enum AVColorTransferCharacteristic 
            public int colorspace; // enum AVColorSpace 
            public int color_range; // enum AVColorRange 
            public int chroma_sample_location; // enum AVChromaLocation 
            public int field_order; // enum AVFieldOrder 
            public int refs;
            public int has_b_frames;
            public int slice_flags;
            public IntPtr draw_horiz_band; // void (*) (struct AVCodecContext *s, const AVFrame* src, int offset[AV_NUM_DATA_POINTERS], int y, int type, int height);
            public IntPtr get_format_fn; // AVPixelFormat (*get_format)(struct AVCodecContext *s, const enum AVPixelFormat * fmt);
            public int max_b_frames;
            public float b_quant_factor;
            public float b_quant_offset;
            public float i_quant_factor;
            public float i_quant_offset;
            public float lumi_masking;
            public float temporal_cplx_masking;
            public float spatial_cplx_masking;
            public float p_masking;
            public float dark_masking;
            public int nsse_weight;
            public int me_cmp;
            public int me_sub_cmp;
            public int mb_cmp;
            public int ildct_cmp;
            public int dia_size;
            public int last_predictor_count;
            public int me_pre_cmp;
            public int pre_dia_size;
            public int me_subpel_quality;
            public int me_range;
            public int mb_decision;
            public IntPtr intra_matrix; // uint16_t*
            public IntPtr inter_matrix; // uint16_t*
            public IntPtr chroma_intra_matrix; // uint16_t*
            public int intra_dc_precision;
            public int mb_lmin;
            public int mb_lmax;
            public int bidir_refine;
            public int keyint_min;
            public int gop_size;
            public int mv0_threshold;
            public int slices;
            public int sample_rate;
            public int sample_fmt;   // AVSampleFormat 
            public AVChannelLayout ch_layout;
            public int frame_size;
            public int block_align;
            public int cutoff;
            public int audio_service_type; // AVAudioServiceType;
            public int request_sample_fmt; // AVSampleFormat;
            public int initial_padding;
            public int trailing_padding;
            public int seek_preroll;
            public IntPtr get_buffer2_fn; // int (*get_buffer2)(struct AVCodecContext *s, AVFrame *frame, int flags);
            public int bit_rate_tolerance;
            public int global_quality;
            public int compression_level;
            public float qcompress;
            public float qblur;
            public int qmin;
            public int qmax;
            public int max_qdiff;
            public int rc_buffer_size;
            public int rc_override_count;
            public IntPtr rc_override; // RcOverride*
            public Int64 rc_max_rate;
            public Int64 rc_min_rate;
            public float rc_max_available_vbv_use;
            public float rc_min_vbv_overflow_use;
            public int rc_initial_buffer_occupancy;
            public int trellis;
            public IntPtr stats_out; // char*
            public IntPtr stats_in; // char*
            public int workaround_bugs;
            public int strict_std_compliance;
            public int error_concealment;
            public int debug;
            public int err_recognition;
            public IntPtr hwaccel; // const struct AVHWAccel *hwaccel;
            public IntPtr hwaccel_context;
            public IntPtr hw_frames_ctx; // AVBufferRef*
            public IntPtr hw_device_ctx; // AVBufferRef*
            public int hwaccel_flags;
            public int extra_hw_frames;
            public UInt64 error0;
            public UInt64 error1;
            public UInt64 error2;
            public UInt64 error3;
            public UInt64 error4;
            public UInt64 error5;
            public UInt64 error6;
            public UInt64 error7;
            public int dct_algo;
            public int idct_algo;
            public int bits_per_coded_sample;
            public int bits_per_raw_sample;
            public int thread_count;
            public int thread_type;
            public int active_thread_type;
            public IntPtr execute_fn; // int (*execute)(struct AVCodecContext *c, int (*func)(struct AVCodecContext *c2, void *arg), void *arg2, int *ret, int count, int size);
            public IntPtr execute2_fn; // int (*execute2)(struct AVCodecContext *c, int (*func)(struct AVCodecContext *c2, void *arg, int jobnr, int threadnr), void *arg2, int *ret, int count);
            public int profile;
            public int level;
            public uint properties;
            public int skip_loop_filter; // AVDiscard;
            public int skip_idct; // AVDiscard;
            public int skip_frame; // AVDiscard;
            public int skip_alpha;
            public int skip_top;
            public int skip_bottom;
            public int lowres;
            public IntPtr codec_descriptor; // const AVCodecDescriptor *
            public IntPtr sub_charenc; // char *
            public int sub_charenc_mode;
            public int subtitle_header_size;
            public IntPtr subtitle_header; // char*
            public IntPtr dump_separator; // uint8_t*
            public IntPtr codec_whitelist; // char*
            public IntPtr coded_side_data; // AVPacketSideData*
            public int nb_coded_side_data;
            public int export_side_data;
            public Int64 max_pixels;
            public int apply_cropping;
            public int discard_damaged_percentage;
            public Int64 max_samples;
            public IntPtr get_encode_buffer_fn; // int (*get_encode_buffer)(struct AVCodecContext *s, AVPacket *pkt, int flags);
            public Int64 frame_num;
            public IntPtr side_data_prefer_packet; //int*
            public uint nb_side_data_prefer_packet;
            public IntPtr decoded_side_data; //AVFrameSideData**
            public int nb_decoded_side_data;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct AVFormatContext
        {
            public IntPtr av_class; // AVClass *av_class;
            public IntPtr iformat; // struct AVInputFormat *iformat;
            public IntPtr oformat; // struct AVOutputFormat *oformat;
            public IntPtr priv_data; // void *priv_data;
            public IntPtr pb; // AVIOContext *pb;
            public int ctx_flags;
            public uint nb_streams;
            public IntPtr streams; // AVStream **streams;
            public uint nb_stream_groups;
            public IntPtr stream_groups; // AVStreamGroup **stream_groups;
            public uint nb_chapters;
            public IntPtr chapters; // AVChapter **chapters;
            public IntPtr url; // char *url;
            public Int64 start_time;
            public Int64 duration;
            public Int64 bit_rate;
            public uint packet_size;
            public int max_delay;
            public int flags;
            public Int64 probesize;
            public Int64 max_analyze_duration;
            public IntPtr key; // uint8_t *key;
            public int keylen;
            public uint nb_programs;
            public IntPtr programs; // AVProgram **programs;
            public int video_codec_id; // AVCodecID 
            public int audio_codec_id; // AVCodecID 
            public int subtitle_codec_id; // AVCodecID 
            public int data_codec_id; // AVCodecID 
            public IntPtr metadata; // AVDictionary *metadata;
            public Int64 start_time_realtime;
            public int fps_probe_size;
            public int error_recognition;
            public IntPtr interrupt_callback; // AVIOInterruptCB
            public int debug;
            public int max_streams;
            public uint max_index_size;
            public uint max_picture_buffer;
            public Int64 max_interleave_delta;
            public int max_ts_probe;
            public int max_chunk_duration;
            public int max_chunk_size;
            public int max_probe_packets;
            public int strict_std_compliance;
            public int event_flags;
            public int avoid_negative_ts;
            public int audio_preload;
            public int use_wallclock_as_timestamps;
            public int skip_estimate_duration_from_pts;
            public int avio_flags;
            public int duration_estimation_method; // AVDurationEstimationMethod 
            public Int64 skip_initial_bytes;
            public uint correct_ts_overflow;
            public int seek2any;
            public int flush_packets;
            public int probe_score;
            public int format_probesize;
            public IntPtr codec_whitelist; // char *codec_whitelist;
            public IntPtr format_whitelist; // char *format_whitelist;
            public IntPtr protocol_whitelist; // char *protocol_whitelist;
            public IntPtr protocol_blacklist; // char *protocol_blacklist;
            public int io_repositioned;
            public IntPtr video_codec;       // struct AVCodec *video_codec;
            public IntPtr audio_codec;       // struct AVCodec *audio_codec;
            public IntPtr subtitle_codec;    // struct AVCodec *subtitle_codec;
            public IntPtr data_codec;        // struct AVCodec *data_codec;
            public int metadata_header_padding;
            public IntPtr opaque; // void *opaque;
            public IntPtr control_message_cb; // av_format_control_message
            public Int64 output_ts_offset;
            public IntPtr dump_separator; // uint8_t *dump_separator;
            public IntPtr io_open; // int (*io_open)(struct AVFormatContext *s, AVIOContext **pb, char *url, int flags, AVDictionary **options);
            public IntPtr io_close2; // int (*io_close2)(struct AVFormatContext *s, AVIOContext *pb);
            public Int64 duration_probesize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AVCodecParameters
        {
            public int codec_type; //AVMediaType
            public int codec_id; //AVCodecID
            public UInt32 codec_tag;
            public IntPtr extradata; //uint8_t*
            public int extradata_size;
            public IntPtr coded_side_data; //AVPacketSideData*
            public int nb_coded_side_data;
            public int format;
            public Int64 bit_rate;
            public int bits_per_coded_sample;
            public int bits_per_raw_sample;
            public int profile;
            public int level;
            public int width;
            public int height;
            public AVRational sample_aspect_ratio;
            public AVRational framerate;
            public int field_order; //AVFieldOrder
            public int color_range; //AVColorRange
            public int color_primaries; //AVColorPrimaries
            public int color_trc; //AVColorTransferCharacteristic
            public int color_space; //AVColorSpace
            public int chroma_location; //AVChromaLocation
            public int video_delay;
            public AVChannelLayout ch_layout;
            public int sample_rate;
            public int block_align;
            public int frame_size;
            public int initial_padding;
            public int trailing_padding;
            public int seek_preroll;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AVStream
        {
            public IntPtr av_class; // AVClass* av_class;
            public int index;
            public int id;
            public IntPtr codecpar; //  AVCodecParameters* codecpar;
            public IntPtr priv_data; // void*
            public AVRational time_base;
            public Int64 start_time;
            public Int64 duration;
            public Int64 nb_frames;
            public int disposition;
            public int discard; // enum AVDiscard 
            public AVRational sample_aspect_ratio;
            public IntPtr metadata; // AVDictionary*
            public AVRational avg_frame_rate;
            public AVPacket attached_pic;
            public int event_flags;
            public AVRational r_frame_rate;
            public int pts_wrap_bits;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AVPacket //fixme
        {
            public int stream_index;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AVFrame
        {
            public IntPtr data0;
            public IntPtr data1;
            public IntPtr data2;
            public IntPtr data3;
            public IntPtr data4;
            public IntPtr data5;
            public IntPtr data6;
            public IntPtr data7;
            public int linesize0;
            public int linesize1;
            public int linesize2;
            public int linesize3;
            public int linesize4;
            public int linesize5;
            public int linesize6;
            public int linesize7;
            public IntPtr extended_data; // uint8_t **
            public int width, height;
            public int nb_samples;
            public int format;
            public int pict_type; //AVPictureType 
            public AVRational sample_aspect_ratio;
            public Int64 pts;
            public Int64 pkt_dts;
            public AVRational time_base;
            public int quality;
            public IntPtr opaque; // void*
            public int repeat_pict;
            public int sample_rate;
            public IntPtr buf0; // AVBufferRef *
            public IntPtr buf1; // AVBufferRef *
            public IntPtr buf2; // AVBufferRef *
            public IntPtr buf3; // AVBufferRef *
            public IntPtr buf4; // AVBufferRef *
            public IntPtr buf5; // AVBufferRef *
            public IntPtr buf6; // AVBufferRef *
            public IntPtr buf7; // AVBufferRef *
            public IntPtr extended_buf; // AVBufferRef **
            public int nb_extended_buf;
            public IntPtr side_data; // AVFrameSideData **
            public int nb_side_data;
            public int flags;
            public int color_range; // AVColorRange 
            public int color_primaries; //AVColorPrimaries 
            public int color_trc; //AVColorTransferCharacteristic 
            public int colorspace; //AVColorSpace 
            public int chroma_location; //AVChromaLocation 
            public Int64 best_effort_timestamp;
            public IntPtr metadata; // AVDictionary *
            public int decode_error_flags;
            public IntPtr hw_frames_ctx; // AVBufferRef *
            public IntPtr opaque_ref; // AVBufferRef *
            public int crop_top;
            public int crop_bottom;
            public int crop_left;
            public int crop_right;
            public IntPtr private_ref; // AVBufferRef*
            public AVChannelLayout ch_layout;
            public Int64 duration;
        }


        public AudioReader(string filePath)
        {
            DecodeAudio(filePath);
        }

        private void DecodeAudio(string filePath)
        {
            IntPtr codecPtr = IntPtr.Zero;
            AVCodecContext codecContext;
            IntPtr codecContextPtr = IntPtr.Zero;
            IntPtr packetPtr = IntPtr.Zero;
            IntPtr framePtr = IntPtr.Zero;
            IntPtr formatContextPtr = IntPtr.Zero; // File handling

            try
            {
                int err;

                err = avformat_open_input(ref formatContextPtr, filePath, IntPtr.Zero, IntPtr.Zero);
                if (err < 0)
                {
                    throw new Exception($"Could not open input file - {err}");
                }
                err = avformat_find_stream_info(formatContextPtr, IntPtr.Zero);
                if (err < 0)
                {
                    throw new Exception($"Could not retrieve stream info - {err}");
                }
                var formatContext = Marshal.PtrToStructure<AVFormatContext>(formatContextPtr);
                //av_dump_format(formatContextPtr, 0, filePath, 0);

                int stream_index = av_find_best_stream(formatContextPtr, AVMediaType.AUDIO, -1, -1, IntPtr.Zero, 0);
                if (stream_index < 0)
                {
                    throw new Exception($"Could find best stream - {stream_index}");
                }

                IntPtr avStreamPtr = Marshal.ReadIntPtr(formatContext.streams, stream_index);
                var avStream = Marshal.PtrToStructure<AVStream>(avStreamPtr);

                var codecPar = Marshal.PtrToStructure<AVCodecParameters>(avStream.codecpar);
                codecPtr = avcodec_find_decoder(codecPar.codec_id);
                if (codecPtr == IntPtr.Zero)
                {
                    throw new Exception("Codec not found.");
                }
                codecContextPtr = avcodec_alloc_context3(codecPtr);
                if (codecContextPtr == IntPtr.Zero)
                {
                    throw new Exception("Failed to allocate codec context.");
                }

                err = avcodec_parameters_to_context(codecContextPtr, avStream.codecpar);
                if (err < 0)
                {
                    throw new Exception($"Could not copy codec parameters to codec context - {err}");
                }

//                if (codecPar.codec_id != (int)AVCodecID.PCM_S16LE)
//                {
                    err = avcodec_open2(codecContextPtr, codecPtr, IntPtr.Zero);
                    if (err < 0)
                    {
                        throw new Exception($"Could not open codec {err}");
                    }
//                }

                codecContext = Marshal.PtrToStructure<AVCodecContext>(codecContextPtr);
                SamplesPerSecond = codecContext.sample_rate;
                ChannelCount = codecContext.ch_layout.nb_channels;

                packetPtr = av_packet_alloc();
                framePtr = av_frame_alloc();

                var channels = new List<double>[ChannelCount];
                for (int loop = 0; loop < ChannelCount; loop++)
                {
                    channels[loop] = new List<double>();
                }

                while (av_read_frame(formatContextPtr, packetPtr) == 0) // Read packets from file
                {
                    var packet = Marshal.PtrToStructure<AVPacket>(packetPtr);
                    if (avcodec_send_packet(codecContextPtr, packetPtr) == 0)
                    {
                        while (avcodec_receive_frame(codecContextPtr, framePtr) == 0)
                        {
                            AVFrame frame = Marshal.PtrToStructure<AVFrame>(framePtr);
                            List<double[]> data = ConvertToDouble(codecContext, frame);
                            for (int ch = 0; ch < channels.Count(); ch++)
                            {
                                channels[ch].AddRange(data[ch]);
                            }
                        }
                    }
                    av_packet_unref(packetPtr);
                }

                Buffer = channels.Select(x => x.ToArray()).ToArray();
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                if (framePtr != IntPtr.Zero) av_frame_free(ref framePtr);
                if (packetPtr != IntPtr.Zero) av_packet_free(ref packetPtr);
                if (codecContextPtr != IntPtr.Zero) avcodec_close(codecContextPtr);
                if (formatContextPtr != IntPtr.Zero) avformat_close_input(ref formatContextPtr);
            }
        }

        private List<double[]> ConvertToDouble(AVCodecContext codecContext, AVFrame frame)
        {
            int numChannels = codecContext.ch_layout.nb_channels;
            int numSamples = frame.nb_samples;

            var channelData = new List<double[]>(numChannels);
            for (int ch = 0; ch < numChannels; ch++)
            {
                channelData.Add(new double[numSamples]); // Initialize each channel buffer
            }

            IntPtr samples = frame.data0; // For non-planar formats, all channels are interleaved in data0

            switch ((AVSampleFormat)codecContext.sample_fmt)
            {
                // planar (not interleaved)
                case AVSampleFormat.U8P:
                case AVSampleFormat.S16P:
                case AVSampleFormat.S32P:
                case AVSampleFormat.FLTP:
                case AVSampleFormat.DBLP:
                case AVSampleFormat.S64P:
                    {
                        for (int ch = 0; ch < numChannels; ch++)
                        {
                            IntPtr channelPtr = ch switch
                            {
                                0 => frame.data0,
                                1 => frame.data1,
                                2 => frame.data2,
                                3 => frame.data3,
                                4 => frame.data4,
                                5 => frame.data5,
                                6 => frame.data6,
                                7 => frame.data7,
                                _ => throw new ArgumentOutOfRangeException(nameof(ch), "Invalid channel index")
                            };

                            switch ((AVSampleFormat)codecContext.sample_fmt)
                            {
                                case AVSampleFormat.U8P:
                                    {
                                        byte[] rawData = new byte[numSamples];
                                        Marshal.Copy(channelPtr, rawData, 0, numSamples);
                                        for (int loop = 0; loop < numSamples; loop++)
                                        {
                                            channelData[ch][loop] = (rawData[loop] - (byte.MaxValue / 2.0)) / (byte.MaxValue / 2.0);
                                        }
                                        break;
                                    }
                                case AVSampleFormat.S16P:
                                    {
                                        short[] rawData = new short[numSamples];
                                        Marshal.Copy(channelPtr, rawData, 0, numSamples);
                                        for (int loop = 0; loop < numSamples; loop++)
                                        {
                                            channelData[ch][loop] = rawData[loop] / (double)short.MaxValue;
                                        }
                                        break;
                                    }
                                case AVSampleFormat.S32P:
                                    {
                                        int[] rawData = new int[numSamples];
                                        Marshal.Copy(channelPtr, rawData, 0, numSamples);
                                        for (int loop = 0; loop < numSamples; loop++)
                                        {
                                            channelData[ch][loop] = rawData[loop] / (double)int.MaxValue;
                                        }
                                        break;
                                    }
                                case AVSampleFormat.FLTP:
                                    {
                                        float[] rawData = new float[numSamples];
                                        Marshal.Copy(channelPtr, rawData, 0, numSamples);
                                        for (int loop = 0; loop < numSamples; loop++)
                                        {
                                            channelData[ch][loop] = rawData[loop];
                                        }
                                        break;
                                    }
                                case AVSampleFormat.DBLP:
                                    {
                                        double[] rawData = new double[numSamples];
                                        Marshal.Copy(channelPtr, rawData, 0, numSamples);
                                        for (int loop = 0; loop < numSamples; loop++)
                                        {
                                            channelData[ch][loop] = rawData[loop];
                                        }
                                        break;
                                    }
                                case AVSampleFormat.S64P:
                                    {
                                        long[] rawData = new long[numSamples];
                                        Marshal.Copy(channelPtr, rawData, 0, numSamples);
                                        for (int loop = 0; loop < numSamples; loop++)
                                        {
                                            channelData[ch][loop] = rawData[loop] / (double)long.MaxValue;
                                        }
                                        break;
                                    }
                            }
                        }
                        break;
                    }

                // interleaved formats
                case AVSampleFormat.U8:
                case AVSampleFormat.S16:
                case AVSampleFormat.S32:
                case AVSampleFormat.FLT:
                case AVSampleFormat.DBL:
                case AVSampleFormat.S64:
                    {
                        int totalSamples = numSamples * numChannels; // Total number of samples in interleaved buffer

                        switch ((AVSampleFormat)codecContext.sample_fmt)
                        {
                            case AVSampleFormat.U8:
                                {
                                    byte[] rawData = new byte[totalSamples];
                                    Marshal.Copy(samples, rawData, 0, totalSamples);
                                    for (int loop = 0; loop < numSamples; loop++)
                                    {
                                        for (int ch = 0; ch < numChannels; ch++)
                                        {
                                            channelData[ch][loop] = (rawData[loop * numChannels + ch] - (byte.MaxValue / 2.0)) / (byte.MaxValue / 2.0);
                                        }
                                    }
                                    break;
                                }
                            case AVSampleFormat.S16:
                                {
                                    short[] rawData = new short[totalSamples];
                                    Marshal.Copy(samples, rawData, 0, totalSamples);
                                    for (int loop = 0; loop < numSamples; loop++)
                                    {
                                        for (int ch = 0; ch < numChannels; ch++)
                                        {
                                            channelData[ch][loop] = rawData[loop * numChannels + ch] / (double)short.MaxValue;
                                        }
                                    }
                                    break;
                                }
                            case AVSampleFormat.S32:
                                {
                                    int[] rawData = new int[totalSamples];
                                    Marshal.Copy(samples, rawData, 0, totalSamples);
                                    for (int loop = 0; loop < numSamples; loop++)
                                    {
                                        for (int ch = 0; ch < numChannels; ch++)
                                        {
                                            channelData[ch][loop] = rawData[loop * numChannels + ch] / (double)int.MaxValue;
                                        }
                                    }
                                    break;
                                }
                            case AVSampleFormat.FLT:
                                {
                                    float[] rawData = new float[totalSamples];
                                    Marshal.Copy(samples, rawData, 0, totalSamples);
                                    for (int loop = 0; loop < numSamples; loop++)
                                    {
                                        for (int ch = 0; ch < numChannels; ch++)
                                        {
                                            channelData[ch][loop] = rawData[loop * numChannels + ch];
                                        }
                                    }
                                    break;
                                }
                            case AVSampleFormat.DBL:
                                {
                                    double[] rawData = new double[totalSamples];
                                    Marshal.Copy(samples, rawData, 0, totalSamples);
                                    for (int loop = 0; loop < numSamples; loop++)
                                    {
                                        for (int ch = 0; ch < numChannels; ch++)
                                        {
                                            channelData[ch][loop] = rawData[loop * numChannels + ch];
                                        }
                                    }
                                    break;
                                }
                            case AVSampleFormat.S64:
                                {
                                    long[] rawData = new long[totalSamples];
                                    Marshal.Copy(samples, rawData, 0, totalSamples);
                                    for (int loop = 0; loop < numSamples; loop++)
                                    {
                                        for (int ch = 0; ch < numChannels; ch++)
                                        {
                                            channelData[ch][loop] = rawData[loop * numChannels + ch] / (double)long.MaxValue;
                                        }
                                    }
                                    break;
                                }
                        }
                        break;
                    }

                default:
                    throw new NotSupportedException($"Unsupported audio format: {codecContext.sample_fmt}");
            }

            return channelData;
        }
    }

    [TestClass]
    public class AudioReaderTests
    {
        [TestMethod]
        public void TestSimple()
        {
            //var a = new AudioReader(@"c:\temp\sweep.m4a");
            //var b = new AudioReader(@"c:\temp\sweep.wav");
        }
    }


}
