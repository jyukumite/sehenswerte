using System.Reflection;

namespace SehensWerte.Controls
{
    public class Splash
    {
        public static void Show(Icon icon, double seconds = 2.5)
        {
            Show(new Icon(icon, -1, -1).ToBitmap());
        }

        public static void Show(Bitmap bitmap, double seconds = 2.5)
        {
            var timer = new System.Windows.Forms.Timer();

            var form = new Form();
            form.Size = new Size(bitmap.Width, bitmap.Height);
            form.FormBorderStyle = FormBorderStyle.None;
            form.StartPosition = FormStartPosition.CenterScreen;
            form.BackgroundImage = bitmap;
            form.TransparencyKey = form.BackColor;
            form.TopMost = true;

            var pic = new PictureBox();
            pic.Image = bitmap;
            pic.Size = new Size(pic.Image.Width, pic.Image.Height);
            pic.Location = new Point(0, 0);
            pic.BackColor = Color.Transparent;
            form.Controls.Add(pic);

            timer.Interval = (int)(seconds * 1000);
            EventHandler close = (s, e) => { timer.Stop(); form.Close(); };

            pic.Click += close;
            form.Click += close;
            timer.Tick += close;
            timer.Start();

            form.Show();
        }
    }
}