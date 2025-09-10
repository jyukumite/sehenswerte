# Sehenswerte

Sehenswerte is a C# .NET 6 WinForms project for visualizing and manipulating data, particularly live or prerecorded CSV data recorded from multiple sensors in an electronic device.

Sehenswerte aims to simplify data, making it accessible to developers and researchers in a powerful visual form.

## Description

The term "Sehenswerte" is derived from the German "worth seeing". This signifies the goal of this project, which is to provide a visually appealing and feature-rich tool for data visualization and manipulation.

This project provides a core library of helper functions, including features such as serial port communication, bit formatting, object dumping, CSV loading and saving, FIR and IIR filters and generators, FFT, statistical filters, signal generators, and various visual controls.

The core library is not intended to replace comprehensive math libraries like SciSharp, but it does provide powerful math functions. These functions can handle various mathematical operations required for data processing and analysis.

The core library supports the Sehens control, which is the oscilloscope for high-speed data acquisition, real-time visualization, and on-screen live or recorded data processing.

The project's main component is the Sehens control, an oscilloscope tool built for high-speed data acquisition, real-time visualization, and on-screen live processing. It provides a  set of visual controls, including the oscilloscope itself, a data mining grid, and a cross-thread logging control.

## Features

* Powerful core library with helper functions for various data processing tasks
* Communication protocols (including serial) for data acquisition
* Bit formatting for convenient data manipulation
* Object dumping and reflection for debugging and inspection
* Process control
* CSV file load and save
* FIR, IIR, SG, and other signal filters and filter generators for signal processing
* FFT (Fast Fourier Transform) for frequency domain analysis
* Statistical filters for data analysis and outlier detection
* Signal generators for creating synthetic test data
* Visual controls, including the oscilloscope, data mining grid, and cross-thread log control

## Support
If you need any assistance or have questions, feel free to reach out through the issue tracker.

## Roadmap
Some of the planned features and improvements include:
* Improved and automated data analytics
* Improved load and save (state) for the scope control
* Enhanced support for additional data file formats
* Integration with external data analysis libraries
* Performance optimizations for real-time data processing
* Expanded set of visual controls
* Improved unit test coverage

## Contributing

We welcome contributions to Sehenswerte from the community.

If you would like to contribute, please follow these guidelines:
* Make an issue to describe the contribution
* Ensure your code adheres to the project's coding standards and conventions.
* Write clear commit messages and provide detailed documentation where necessary.
* Submit a pull request, e.g. on branch bugfix/issue-123, describing the changes you have made and their purpose.
* By contributing to Sehenswerte, you agree that your contributions will be licensed under the same license as the project.

## License

This software is licensed under the MIT License with additional terms.

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the “Software”), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

1. The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
2. When using or distributing the Software, attribution to the project shall be provided in an obvious place.
3. Improvements and bug fixes made to the Software should be contributed back to the public code for the benefit of the community and under the same license.
4. Copyright of the Software remains with the author, jyukumite.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF, OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
