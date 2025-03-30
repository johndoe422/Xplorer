using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;

namespace Xplorer
{
    static class Program
    {
        private static Mutex mutex = null;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            const string appName = "03817d52-b2bd-433d-b207-751fea6737d6";
            bool createdNew;

            mutex = new Mutex(true, appName, out createdNew);

            if (!createdNew)
            {
                MessageBox.Show("An instance of this application is already running.");
                return;
            }

            // Start the update check in a separate thread
            Thread updateThread = new Thread(ShowFormIfUpdateAvailable);
            updateThread.IsBackground = true;
            updateThread.Start();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }

        static void ShowFormIfUpdateAvailable()
        {
            Autoupdate updFrm = new Autoupdate();
            updFrm.CheckForUpdateAsync();

            if (updFrm.UpdateAvailable)
            {
                updFrm.ShowDialog();
            }
        }
    }
}
