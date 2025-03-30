using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Xplorer
{
    public partial class Autoupdate : Form
    {
        public bool UpdateAvailable { get; set; } = false;

        public Autoupdate()
        {
            InitializeComponent();
        }

        private void buttonInstall_Click(object sender, EventArgs e)
        {

        }

        private void buttonClose_Click(object sender, EventArgs e)
        {

        }

        public void CheckForUpdateAsync()
        {
            System.Threading.Thread.Sleep(5000);
            this.UpdateAvailable = true;
        }
    }
}
