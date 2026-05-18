using System;
using System.Data;
using System.Data.SqlClient;
using System.Windows.Forms;
using PensionMgmt.Data;
using PensionMgmt.Session;

namespace PensionMgmt.Forms
{
    public partial class LoginForm : Form
    {
        private readonly DbAccess _db = new DbAccess();
       
        public LoginForm()
        {
            InitializeComponent();
        }

        private void ShowUsernameError(string msg)
        {
            lblUsernameError.Text = msg;
            lblUsernameError.Visible = true;
        }

        private void ShowPasswordError(string msg)
        {
            lblPasswordError.Text = msg;
            lblPasswordError.Visible = true;
        }

        private void ClearErrors()
        {
            lblUsernameError.Text = string.Empty;
            lblUsernameError.Visible = false;
            lblPasswordError.Text = string.Empty;
            lblPasswordError.Visible = false;
        }

        private bool ValidateInput()
        {
            ClearErrors();
            bool valid = true;

            if (string.IsNullOrWhiteSpace(txtUsername.Text))
            {
                ShowUsernameError("Please enter your Employee ID.");
                valid = false;
            }
            else if (txtUsername.Text.Trim().Length != 10 || !long.TryParse(txtUsername.Text.Trim(), out _))
            {
                ShowUsernameError("Employee ID must be exactly 10 digits.");
                valid = false;
            }

            if (string.IsNullOrWhiteSpace(txtPassword.Text))
            {
                ShowPasswordError("Please enter your Password.");
                valid = false;
            }
            else if (txtPassword.Text.Trim().Length != 6 || !int.TryParse(txtPassword.Text.Trim(), out _))
            {
                ShowPasswordError("Password must be exactly 6 digits.");
                valid = false;
            }

            if (!valid)
            {
                if (lblUsernameError.Visible) txtUsername.Focus();
                else txtPassword.Focus();
            }

            return valid;
        }

        private DataTable TryLogin(string table, string username, string password)
        {
            string sql = string.Format("SELECT UserName FROM [{0}] WHERE UserName = @u AND Password = @p", table);
            return _db.GetTable(sql,
                new SqlParameter("@u", username),
                new SqlParameter("@p", password));
        }

        private void btnLogin_Click(object sender, EventArgs e)
        {
            if (!ValidateInput()) return;

            string user = txtUsername.Text.Trim();
            string pass = txtPassword.Text.Trim();

            string[] tables = { "SystemAdmin", "PensionAdmin", "Manager", "PensionHolder" };
            UserRole[] roles = { UserRole.SystemAdmin, UserRole.PensionAdmin, UserRole.PensionManager, UserRole.PensionHolder };

            for (int i = 0; i < tables.Length; i++)
            {
                DataTable dt = TryLogin(tables[i], user, pass);
                if (dt.Rows.Count == 1)
                {
                    CurrentUser.Username = user;
                    CurrentUser.Role = roles[i];

                    object nameObj = _db.ExecuteScalar(
                        "SELECT FullName FROM Users WHERE EmployeeId = @u",
                        new SqlParameter("@u", user));
                    CurrentUser.FullName = (nameObj != null && nameObj != DBNull.Value)
                        ? nameObj.ToString()
                        : user;

                    MessageBox.Show(
                    "Welcome, " + CurrentUser.FullName + "!\nYou have logged in successfully.",
                    "Login Successful",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                    MainForm main = new MainForm();
                    main.Show();
                    try
                    {
                        _db.Execute(
                            "INSERT INTO AuditLog (ActionType, TargetId, TargetName, Description, PerformedBy, PerformedAt) " +
                            "VALUES (@act, @tid, @tname, @det, @by, @at)",
                            new SqlParameter("@act",   "Login"),
                            new SqlParameter("@tid",   user),
                            new SqlParameter("@tname", CurrentUser.FullName),
                            new SqlParameter("@det",   string.Format("User '{0}' logged in as {1}", CurrentUser.FullName, roles[i].ToString())),
                            new SqlParameter("@by",    user),
                            new SqlParameter("@at",    DateTime.Now));
                    }
                    catch { }
                    this.Hide();
                    return;
                }
            }

            object pending = _db.ExecuteScalar(
                "SELECT COUNT(*) FROM PendingRegistrations WHERE EmployeeId = @u AND Status = 'Pending'",
                new SqlParameter("@u", user));

            if (Convert.ToInt32(pending) > 0)
            {
                ShowUsernameError("Your registration is pending approval by an administrator.");
                return;
            }

            ShowUsernameError("Invalid Employee ID or Password.");
            ShowPasswordError("Please check your credentials and try again.");
        }

        private void btnRegister_Click(object sender, EventArgs e)
        {
            ClearErrors();
            new RegisterForm().Show();
            this.Hide();
        }

        //private void btnForgotPassword_Click(object sender, EventArgs e)
        //{
        //  ClearErrors();
        //ShowPasswordError("Please contact your System Administrator to reset your password.");
        //}


        private void btnForgotPassword_Click(object sender, EventArgs e)
        {
            ClearErrors();

            // Step 1: Ask for Employee ID
            string empId = ShowInputDialog("Enter your Employee ID (10 digits):", "Forgot Password");
            if (string.IsNullOrWhiteSpace(empId)) return;

            empId = empId.Trim();

            if (empId.Length != 10 || !long.TryParse(empId, out long temp))
            {
                MessageBox.Show("Employee ID must be exactly 10 digits.", "Invalid Input",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Step 2: Ask for Date of Birth
            string dobStr = ShowInputDialog("Enter your Date of Birth (yyyy-MM-dd):", "Forgot Password");
            if (string.IsNullOrWhiteSpace(dobStr)) return;

            if (!DateTime.TryParseExact(dobStr.Trim(), "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out DateTime dob))
            {
                MessageBox.Show("Invalid date format. Please use yyyy-MM-dd (e.g. 1990-05-20).",
                    "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Step 3: Verify Employee ID + Date of Birth against Employees table
            DataTable dt = _db.GetTable(
                "SELECT EmployeeId FROM Employees WHERE EmployeeId = @id AND DateOfBirth = @dob",
                new SqlParameter("@id", empId),
                new SqlParameter("@dob", dob.Date));

            if (dt.Rows.Count == 0)
            {
                MessageBox.Show("Verification failed. Employee ID and Date of Birth do not match.",
                    "Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Step 4: Fetch password from Users table
            object passObj = _db.ExecuteScalar(
                "SELECT Password FROM Users WHERE EmployeeId = @id",
                new SqlParameter("@id", empId));

            if (passObj == null || passObj == DBNull.Value)
            {
                MessageBox.Show("No account found for this Employee ID. Please contact your administrator.",
                    "Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Step 5: Show the password
            MessageBox.Show("Your password is: " + passObj.ToString(),
                "Password Retrieved", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private string ShowInputDialog(string prompt, string title)
        {
            Form inputForm = new Form();
            inputForm.Text = title;
            inputForm.Size = new System.Drawing.Size(360, 150);
            inputForm.StartPosition = FormStartPosition.CenterParent;
            inputForm.FormBorderStyle = FormBorderStyle.FixedDialog;
            inputForm.MaximizeBox = false;
            inputForm.MinimizeBox = false;

            Label lbl = new Label();
            lbl.Text = prompt;
            lbl.SetBounds(10, 15, 330, 20);

            TextBox txt = new TextBox();
            txt.SetBounds(10, 40, 330, 25);

            Button btnOk = new Button();
            btnOk.Text = "OK";
            btnOk.SetBounds(180, 75, 75, 28);
            btnOk.DialogResult = DialogResult.OK;

            Button btnCancel = new Button();
            btnCancel.Text = "Cancel";
            btnCancel.SetBounds(265, 75, 75, 28);
            btnCancel.DialogResult = DialogResult.Cancel;

            inputForm.Controls.AddRange(new Control[] { lbl, txt, btnOk, btnCancel });
            inputForm.AcceptButton = btnOk;
            inputForm.CancelButton = btnCancel;

            return inputForm.ShowDialog() == DialogResult.OK ? txt.Text : null;
        }


        private void LoginForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (Application.OpenForms.Count == 0)
                Application.Exit();
        }
    }
}