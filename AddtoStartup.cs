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
    public partial class AddtoStartup : Form
    {
        public bool DoNotShowAgain { get; private set; } = false;
        public bool AddToStartup { get; private set; } = false;

        public AddtoStartup()
        {
            InitializeComponent();
        }

        private void buttonYes_Click(object sender, EventArgs e)
        {
            AddToStartup = true;
            DoNotShowAgain = chkDoNotShow.Checked;
            this.DialogResult = DialogResult.Yes;
            this.Close();
        }

        private void buttonNo_Click(object sender, EventArgs e)
        {
            AddToStartup = false;
            DoNotShowAgain = chkDoNotShow.Checked;
            this.DialogResult = DialogResult.No;
            this.Close();
        }

    }
}
