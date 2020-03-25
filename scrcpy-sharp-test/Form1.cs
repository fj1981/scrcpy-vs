using SharpScrcpy;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace scrcpy_sharp_test
{
  public partial class Form1 : Form
  {
    public Form1()
    {
      InitializeComponent();
      ScrcpySrvSharp.Get().OnDeviceConnected += OnDeviceConnected;
      ScrcpySrvSharp.Get().OnImageArrived += OnImageReady;
      ScrcpySrvSharp.Get().RunServer(9090);
    }

    void OnDeviceConnected(object sender, UsbDeviceEventArgs e)
    {
      ScrcpySrvSharp.Get().StartScrcpyServer(e);
    }

    void OnImageReady(object sender, Image image)
    {
      this.Invoke((EventHandler)delegate
      {
        pictureBox1.Image = image;
      });

    }

  }
}
