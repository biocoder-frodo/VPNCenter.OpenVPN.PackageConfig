using System.Collections;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Threading.Tasks;

namespace VPNCenter.OpenVPN.PackageConfig
{
    interface IKeyboardInteractiveKeyPress
    {
        public event KeyPressEventHandler KeyPress;
    }
    public partial class Form1 : Form, IKeyboardInteractiveKeyPress
    {
        int length = 0;
        public Form1()
        {
            InitializeComponent();
        }
        private void HandleKeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == '\b')
            {
                if (length > 0) length--;
            }
            else
            {
                if (e.KeyChar != '\r') length++;
            }

            textBox1.Text = new String('*', length);

        }

    }
}