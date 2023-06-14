using DiskStationManager.SecureShell;
using Renci.SshNet;
using System.Security.Cryptography;
using Extensions;
using static System.Configuration.UserSectionHandler;
using static VPNCenter.OpenVPN.PackageConfig.Settings;

namespace VPNCenter.OpenVPN.PackageConfig
{
    public partial class Form2 : Form
    {
        public Form2()
        {
            InitializeComponent();
        }

        private async Task<bool> GetPassword()
        {
            using (var pw = new Form1())
            {
                pw.KeyPress += Pw_KeyPress;
                pw.ShowDialog();

                pw.KeyPress -= Pw_KeyPress;

            }
            return true;
        }

        private void Pw_KeyPress(object? sender, KeyPressEventArgs e)
        {
            //throw new NotImplementedException();

        }

        private void Form2_Click(object sender, EventArgs e)
        {
            using (var pw = new Form1())
            {
                pw.KeyPress += Pw_KeyPress;
                pw.ShowDialog();

                pw.KeyPress -= Pw_KeyPress;

            }
            //return true;
            //Task.Run(GetPasswordAsync);
        }
        private void GetPasswordAsync()
        {
            var result = GetPassword().Result;
        }

    }
}
