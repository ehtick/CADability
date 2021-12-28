using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CADability.App
{
    public partial class MainMdiForm : Form
    {
        public MainMdiForm()
        {
            InitializeComponent();
        }

        private void btnCloseAll_Click(object sender, EventArgs e)
        {
            foreach (Form frm in this.MdiChildren)
                frm.Close();
        }

        private void btnAddOne_Click(object sender, EventArgs e)
        {
            ChildMdiForm frm = new ChildMdiForm();
            frm.MdiParent = this;
            frm.Show();
        }

        private void btnAddTen_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < 10; i++)
            {
                ChildMdiForm frm = new ChildMdiForm();
                frm.MdiParent = this;
                frm.Show();
            }
        }
    }
}
