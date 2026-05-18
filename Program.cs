using System;
using System.Windows.Forms;
using PensionMgmt.Data;
using PensionMgmt.Forms;

namespace PensionMgmt
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Verify DB connection before opening the app
            DbAccess db = new DbAccess();
            if (!db.TestConnection())
            {
                MessageBox.Show(
                    "Cannot connect to the database.\n\n" +
                    "Please:\n" +
                    "1. Ensure SQL Server Express is running.\n" +
                    "2. Run DatabaseSetup.sql in SSMS.\n" +
                    "3. Update the connection string in Data\\DbAccess.cs.",
                    "Connection Failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            Application.Run(new LoginForm());
        }
    }
}
