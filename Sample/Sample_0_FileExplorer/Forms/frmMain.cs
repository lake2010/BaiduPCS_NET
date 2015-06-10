﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;

using BaiduPCS_NET;

namespace FileExplorer
{
    public partial class frmMain : Form
    {
        private BaiduPCS pcs = null;
        private Stack<string> history = null;
        private Stack<string> next = null;
        private string currentPath = string.Empty; //当前路径
        private string lastSearchPath = string.Empty;
        private ListViewItem contextItem = null;
        private PcsFileInfo source;
        private bool isMove = false;

        public frmMain()
        {
            InitializeComponent();

            lvFileList.DoubleClick += lvFileList_DoubleClick;

            txSearchKeyword.GotFocus += txSearchKeyword_GotFocus;
            txSearchKeyword.LostFocus += txSearchKeyword_LostFocus;
            txSearchKeyword.KeyPress += txSearchKeyword_KeyPress;

            cmbLocation.KeyPress += cmbLocation_KeyPress;

            history = new Stack<string>();
            next = new Stack<string>();
            source = new PcsFileInfo();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (pcs == null)
            {
                bool succ = true;
                ExecTask("Login", "Logging in ...",
                    new ThreadStart(delegate()
                    {
                        try
                        {
                            if (!createBaiduPCS())
                            {
                                succ = false;
                                MessageBox.Show("Can't create BaiduPCS");
                                Application.Exit();
                                return;
                            }
                        }
                        catch(Exception ex)
                        {
                            succ = false;
                            MessageBox.Show("Can't create BaiduPCS: " + ex.Message);
                            Application.Exit();
                            return;
                        }
                    }),
                    new ThreadStart(delegate()
                    {
                        if (!succ)
                            return;
                        this.Invoke(new AnonymousFunction(delegate()
                        {
                            Go("/");
                        }));
                    }));
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            DisposePCS();
        }

        private void txSearchKeyword_LostFocus(object sender, EventArgs e)
        {
            string keyword = txSearchKeyword.Text.Trim();
            if (string.IsNullOrEmpty(keyword) || string.Equals(keyword, "Search by filename", StringComparison.InvariantCultureIgnoreCase))
            {
                txSearchKeyword.Text = "Search by filename";
                txSearchKeyword.ForeColor = Color.Gray;
            }
        }

        private void txSearchKeyword_GotFocus(object sender, EventArgs e)
        {
            string keyword = txSearchKeyword.Text.Trim();
            if (string.Equals(keyword, "Search by filename", StringComparison.InvariantCultureIgnoreCase))
            {
                txSearchKeyword.Text = "";
            }
            txSearchKeyword.ForeColor = Color.Black;
        }

        private void txSearchKeyword_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                btnSearch_Click(sender, e);
            }
        }

        private void cmbLocation_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                string newPath = cmbLocation.Text;
                Go(newPath);
            }
        }

        private void lvFileList_DoubleClick(object sender, EventArgs e)
        {
            if (lvFileList.SelectedItems.Count == 0)
                return;
            PcsFileInfo fileInfo = (PcsFileInfo)lvFileList.SelectedItems[0].Tag;
            if(fileInfo.isdir)
            {
                Go(fileInfo.path);
            }
        }

        private void btnGo_Click(object sender, EventArgs e)
        {
            string newPath = cmbLocation.Text;
            Go(newPath);
        }

        private void btnBack_Click(object sender, EventArgs e)
        {
            Back();
        }

        private void btnNext_Click(object sender, EventArgs e)
        {
            Next();
        }

        private void btnUp_Click(object sender, EventArgs e)
        {
            Up();
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            RefreshFileList();
        }

        private void btnUpload_Click(object sender, EventArgs e)
        {
            PcsFileInfo fileinfo = new PcsFileInfo();
            fileinfo.path = currentPath;
            fileinfo.isdir = true;
            if (UploadFile(fileinfo))
            {
                if (string.Equals(fileinfo.path, currentPath, StringComparison.InvariantCultureIgnoreCase))
                    RefreshFileList();
            }
        }

        private void btnNewFolder_Click(object sender, EventArgs e)
        {
            if (CreateNewFolder(currentPath))
            {
                RefreshFileList();
            }
        }

        private void btnSettings_Click(object sender, EventArgs e)
        {

        }

        private void btnSearch_Click(object sender, EventArgs e)
        {
            string keywords = txSearchKeyword.Text.Trim();
            if (string.IsNullOrEmpty(keywords) || string.Equals(keywords, "Search by filename", StringComparison.InvariantCultureIgnoreCase))
            {
                MessageBox.Show("Please input the filename.");
                txSearchKeyword.Focus();
                return;
            }
            if (keywords.Length <= 3)
            {
                if (MessageBox.Show("The keywords is too short, the result maybe very large, you maybe need to wait a long time, are you sure to continue?",
                    "Keywords too short", MessageBoxButtons.OKCancel) != System.Windows.Forms.DialogResult.OK)
                {
                    txSearchKeyword.Focus();
                    txSearchKeyword.SelectAll();
                    return;
                }
            }
            string path = currentPath;
            if (!path.StartsWith("/"))
                path = lastSearchPath;
            if (SearchFiles(path, keywords))
            {
                if (!string.IsNullOrEmpty(currentPath) && currentPath.StartsWith("/"))
                    history.Push(currentPath);
                next.Clear();
                lastSearchPath = currentPath;
                currentPath = cmbLocation.Text;
                RefreshControls();
            }
        }

        private bool createBaiduPCS()
        {
            string cookiefilename = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".pcs", "cookie.txt");
            string dir = System.IO.Path.GetDirectoryName(cookiefilename);
            if (!System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);
            pcs = BaiduPCS.pcs_create(cookiefilename);
            if (pcs == null)
                return false;
            if (pcs.isLogin() != PcsRes.PCS_LOGIN)
            {
                System.Windows.Forms.DialogResult dr = System.Windows.Forms.DialogResult.None;
                this.Invoke(new AnonymousFunction(delegate()
                {
                    frmLogin frm = new frmLogin(pcs);
                    dr = frm.ShowDialog();
                }));
                if (dr != System.Windows.Forms.DialogResult.OK)
                    return false;
            }
            long quota, used;
            pcs.quota(out quota, out used);
            string title = pcs.getUID() + "'s Baidu Cloud Disk " + Utils.HumanReadableSize(used) + "/" + Utils.HumanReadableSize(quota);
            this.Invoke(new AnonymousFunction(delegate() {
                Text = title;
                lblStatus.Text = title;
            }));
            return true;
        }

        private void DisposePCS()
        {
            if (pcs != null)
            {
                try { pcs.Dispose(); }
                catch { }
                pcs = null;
            }
        }

        private void ExecTask(ThreadStart task, ThreadStart onTaskFinished = null)
        {
            ExecTask("Processing", "Processing...\r\nPlease wait...", task, onTaskFinished);
        }

        private void ExecTask(string title, string description, ThreadStart task, ThreadStart onTaskFinished = null)
        {
            if(this.InvokeRequired)
            {
                this.Invoke(new AnonymousFunction(delegate() {
                    frmWaiting frm = new frmWaiting(title, description);
                    frm.Exec(task, onTaskFinished);
                }));
            }
            else
            {
                frmWaiting frm = new frmWaiting(title, description);
                frm.Exec(task, onTaskFinished);
            }
        }

        private void Go(string newPath)
        {
            if (newPath == "")
                return;
            if (!newPath.StartsWith("/"))
            {
                MessageBox.Show("Must be the full path. e.g. \"/movies\"");
                return;
            }
            if(ListFiles(newPath))
            {
                if (!string.IsNullOrEmpty(currentPath) && currentPath.StartsWith("/"))
                    history.Push(currentPath);
                next.Clear();
                currentPath = newPath;
                RefreshControls();
            }
        }

        private void Back()
        {
            string path = history.Pop();
            if (!string.IsNullOrEmpty(path))
            {
                if (ListFiles(path))
                {
                    if (!string.IsNullOrEmpty(currentPath) && currentPath.StartsWith("/"))
                        next.Push(currentPath);
                    currentPath = path;
                    RefreshControls();
                }
            }
        }

        private void Next()
        {
            string path = next.Pop();
            if (!string.IsNullOrEmpty(path))
            {
                if (ListFiles(path))
                {
                    if (!string.IsNullOrEmpty(currentPath) && currentPath.StartsWith("/"))
                        history.Push(currentPath);
                    currentPath = path;
                    RefreshControls();
                }
            }
        }

        private void Up()
        {
            if (!currentPath.StartsWith("/"))
                return;
            string path = Path.GetDirectoryName(currentPath).Replace("\\", "/");
            if (!string.IsNullOrEmpty(path) && path != currentPath)
            {
                Go(path);
            }
        }

        private void RefreshFileList()
        {
            ListFiles(currentPath);
        }

        private bool ListFiles(string path)
        {
            PcsFileInfo[] list = null;
            string errmsg = null;
            ExecTask(new ThreadStart(delegate()
            {
                try
                {
                    list = pcs.list(path, 1, int.MaxValue);
                    if (list == null)
                        errmsg = pcs.getError();
                }
                catch (Exception ex)
                {
                    errmsg = ex.Message;
                }
            }));
            if (errmsg != null)
            {
                MessageBox.Show("Can't list the file for \"" + path + "\": " + errmsg);
                return false;
            }
            cmbLocation.Text = path;
            BindFileList(list);
            return true;
        }

        private bool SearchFiles(string path, string keyword)
        {
            PcsFileInfo[] list = null;
            string errmsg = null;
            ExecTask(new ThreadStart(delegate()
            {
                try
                {
                    list = pcs.search(path, keyword, true);
                    if (list == null)
                        errmsg = pcs.getError();
                    else
                    {
                        //排序
                        List<PcsFileInfo> l = new List<PcsFileInfo>(list);
                        l.Sort(new PcsFileInfoComparer());
                        list = l.ToArray();
                    }
                }
                catch (Exception ex)
                {
                    errmsg = ex.Message;
                }
            }));
            if (errmsg != null)
            {
                MessageBox.Show("Can't search \"" + keyword + "\" in \"" + path + "\": " + errmsg);
                return false;
            }
            cmbLocation.Text = "Search \"" + keyword + "\" in \"" + path + "\"";
            BindFileList(list);
            return true;
        }

        private void BindFileList(PcsFileInfo[] list)
        {
            lvFileList.Items.Clear();
            bigImageList.Images.Clear();
            smallImageList.Images.Clear();
            Icon icon = SystemIcon.GetFolderIcon(true);
            if (icon != null)
                bigImageList.Images.Add(icon);
            else
                bigImageList.Images.Add(Properties.Resources.generic_folder);
            icon = SystemIcon.GetFolderIcon(false);
            if (icon != null)
                smallImageList.Images.Add(icon);
            else
                smallImageList.Images.Add(Properties.Resources.generic_folder);
            if (list == null)
                return;

            List<ListViewItem> items = new List<ListViewItem>();
            foreach (PcsFileInfo file in list)
            {
                ListViewItem item = new ListViewItem(file.server_filename);
                items.Add(item);
                item.ToolTipText = file.path;
                item.Tag = file;
                if (file.isdir)
                {
                    item.SubItems.Add("");
                    item.SubItems.Add("Directory");
                    item.SubItems.Add(Utils.FromUnixTimeStamp(file.server_mtime).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
                    item.ImageIndex = 0;
                }
                else
                {
                    item.SubItems.Add(Utils.HumanReadableSize(file.size));
                    item.SubItems.Add(Path.GetExtension(file.server_filename));
                    item.SubItems.Add(Utils.FromUnixTimeStamp(file.server_mtime).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
                    icon = SystemIcon.GetIcon(file, true);
                    if (icon != null)
                        bigImageList.Images.Add(file.server_filename, icon);
                    else
                        bigImageList.Images.Add(file.server_filename, Properties.Resources.generic_file);
                    icon = SystemIcon.GetIcon(file, false);
                    if (icon != null)
                        smallImageList.Images.Add(file.server_filename, icon);
                    else
                        smallImageList.Images.Add(file.server_filename, Properties.Resources.generic_file);
                    item.ImageKey = file.server_filename;
                }
            }
            lvFileList.Items.AddRange(items.ToArray());
            lblStatus.Text = lvFileList.Items.Count.ToString() + " items"; //更新状态栏
        }

        private void RefreshControls()
        {
            btnBack.Enabled = history.Count > 0;
            btnNext.Enabled = next.Count > 0;
            if (currentPath.StartsWith("/"))
            {
                string uppath = Path.GetDirectoryName(currentPath);
                btnUp.Enabled = !string.IsNullOrEmpty(uppath) && uppath != currentPath;
            }
            else
                btnUp.Enabled = false;
        }

        private PcsFileInfo GetFileMetaInformation(string path)
        {
            PcsFileInfo fileinfo = new PcsFileInfo();
            string errmsg = null;
            ExecTask(new ThreadStart(delegate()
            {
                try
                {
                    fileinfo = pcs.meta(path);
                    if (fileinfo.IsEmpty)
                    {
                        errmsg = pcs.getError();
                    }
                }
                catch (Exception ex)
                {
                    errmsg = ex.Message;
                }
            }));
            if (errmsg != null)
            {
                MessageBox.Show("Can't get meta information for \"" + path + "\": " + errmsg);
            }
            return fileinfo;
        }

        private void ShowFileMetaInformation(PcsFileInfo fileinfo)
        {
            frmMetaInformation frm = new frmMetaInformation(fileinfo);
            frm.ShowDialog();
        }

        private bool MoveFile(PcsFileInfo from, PcsFileInfo to)
        {
            PcsPanApiRes ar = new PcsPanApiRes();
            string errmsg = null;
            SPair spair = new SPair(
                        from.path,
                        to.path + "/" + from.server_filename);
            ExecTask(new ThreadStart(delegate()
            {
                try
                {
                    ar = pcs.move(new SPair[] { spair });
                    if (ar.error != 0)
                    {
                        errmsg = pcs.getError();
                    }
                }
                catch (Exception ex)
                {
                    errmsg = ex.Message;
                }
            }));
            if (errmsg != null)
            {
                MessageBox.Show("Can't move the file (\"" + spair.str1 + "\" => \"" + spair.str2 + "\"): " + errmsg);
                return false;
            }
            return true;
        }

        private bool CopyFile(PcsFileInfo from, PcsFileInfo to)
        {
            PcsPanApiRes ar = new PcsPanApiRes();
            string errmsg = null;
            SPair spair = new SPair(
                        from.path,
                        to.path + "/" + from.server_filename);
            ExecTask(new ThreadStart(delegate()
            {
                try
                {
                    ar = pcs.copy(new SPair[] { spair });
                    if (ar.error != 0)
                    {
                        errmsg = pcs.getError();
                    }
                }
                catch (Exception ex)
                {
                    errmsg = ex.Message;
                }
            }));
            if (errmsg != null)
            {
                MessageBox.Show("Can't copy the file (\"" + spair.str1 + "\" => \"" + spair.str2 + "\"): " + errmsg);
                return false;
            }
            return true;
        }

        private bool CreateNewFolder(string parentPath)
        {
            frmFileName frmFN = new frmFileName(string.Empty);
            if(frmFN.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string filename = frmFN.FileName;
                string errmsg = null;
                ExecTask(new ThreadStart(delegate()
                {
                    try
                    {
                        PcsRes rc = pcs.mkdir(parentPath + "/" + filename);
                        if (rc != PcsRes.PCS_OK)
                        {
                            errmsg = pcs.getError();
                        }
                    }
                    catch (Exception ex)
                    {
                        errmsg = ex.Message;
                    }
                }));
                if (errmsg != null)
                {
                    MessageBox.Show("Can't create the directory \"" + parentPath + "/" + filename + "\": " + errmsg);
                    return false;
                }
                return true;
            }
            return false;
        }

        private bool DeleteFile(PcsFileInfo fileinfo)
        {
            if(MessageBox.Show("Are you sure you want to delete " + fileinfo.path + "?", "Delete", MessageBoxButtons.OKCancel) == System.Windows.Forms.DialogResult.OK)
            {
                string errmsg = null;
                ExecTask(new ThreadStart(delegate()
                {
                    try
                    {
                        PcsPanApiRes rc = pcs.delete(new string[] { fileinfo.path });
                        if (rc.error != 0)
                        {
                            errmsg = pcs.getError();
                        }
                    }
                    catch (Exception ex)
                    {
                        errmsg = ex.Message;
                    }
                }));
                if (errmsg != null)
                {
                    MessageBox.Show("Can't delete \"" + fileinfo.path + "\": " + errmsg);
                    return false;
                }
                return true;
            }
            return false;
        }

        private bool RenameFile(PcsFileInfo fileinfo)
        {
            frmFileName frmFN = new frmFileName(fileinfo.server_filename);
            if(frmFN.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string filename = frmFN.FileName;
                SPair spair = new SPair(
                            fileinfo.path,
                            filename);
                PcsPanApiRes ar = new PcsPanApiRes();
                string errmsg = null;
                ExecTask(new ThreadStart(delegate()
                {
                    try
                    {
                        ar = pcs.rename(new SPair[] { spair });
                        if (ar.error != 0)
                        {
                            errmsg = pcs.getError();
                        }
                    }
                    catch (Exception ex)
                    {
                        errmsg = ex.Message;
                    }
                }));
                if (errmsg != null)
                {
                    MessageBox.Show("Can't rename (\"" + spair.str1 + "\" => \"" + spair.str2 + "\"): " + errmsg);
                    return false;
                }
                return true;
            }
            return false;
        }

        private bool DownloadFile(PcsFileInfo fileinfo)
        {
            return false;
        }

        private bool UploadFile(PcsFileInfo to)
        {
            return false;
        }

        #region 上下文菜单代码

        private void contextMenuStrip1_Opening(object sender, CancelEventArgs e)
        {
            Point point = lvFileList.PointToClient(Cursor.Position);
            ListViewItem item = lvFileList.GetItemAt(point.X, point.Y);   //获得鼠标坐标处的ListViewItem
            contextItem = item;
            if (item == null)   //当前位置没有ListViewItem
            {
                if(!currentPath.StartsWith("/"))
                {
                    openToolStripMenuItem1.Visible = false;
                    uploadFileToolStripMenuItem.Visible = false;
                    viewToolStripMenuItem1.Visible = true;
                    refreshToolStripMenuItem1.Visible = false;
                    renameToolStripMenuItem.Visible = false;
                    cutToolStripMenuItem1.Visible = false;
                    copyToolStripMenuItem1.Visible = false;
                    deleteToolStripMenuItem1.Visible = false;
                    parseToolStripMenuItem1.Visible = false;
                    mkdirToolStripMenuItem1.Visible = false;
                    attributesToolStripMenuItem1.Visible = false;

                    toolStripMenuItem3.Visible = false;
                    toolStripMenuItem4.Visible = false;
                }
                else
                {
                    openToolStripMenuItem1.Visible = false;
                    uploadFileToolStripMenuItem.Visible = true;
                    viewToolStripMenuItem1.Visible = true;
                    refreshToolStripMenuItem1.Visible = true;
                    renameToolStripMenuItem.Visible = false;
                    cutToolStripMenuItem1.Visible = false;
                    copyToolStripMenuItem1.Visible = false;
                    deleteToolStripMenuItem1.Visible = false;
                    parseToolStripMenuItem1.Visible = !source.IsEmpty;
                    mkdirToolStripMenuItem1.Visible = true;
                    attributesToolStripMenuItem1.Visible = true;

                    toolStripMenuItem3.Visible = true;
                    toolStripMenuItem4.Visible = true;
                }
            }
            else    //有
            {
                PcsFileInfo fileinfo = (PcsFileInfo)item.Tag;
                if (fileinfo.isdir)
                {
                    openToolStripMenuItem1.Text = "Open Directory";
                    uploadFileToolStripMenuItem.Visible = true;
                }
                else
                {
                    openToolStripMenuItem1.Text = "Download file";
                    uploadFileToolStripMenuItem.Visible = false;
                }
                openToolStripMenuItem1.Visible = true;
                viewToolStripMenuItem1.Visible = false;
                refreshToolStripMenuItem1.Visible = false;
                renameToolStripMenuItem.Visible = true;
                cutToolStripMenuItem1.Visible = true;
                copyToolStripMenuItem1.Visible = true;
                deleteToolStripMenuItem1.Visible = true;
                parseToolStripMenuItem1.Visible = !source.IsEmpty && fileinfo.isdir;
                mkdirToolStripMenuItem1.Visible = false;
                attributesToolStripMenuItem1.Visible = true;

                toolStripMenuItem3.Visible = true;
                toolStripMenuItem4.Visible = true;
            }
        }

        private void openToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (contextItem == null)
                return;
            PcsFileInfo fileinfo = (PcsFileInfo)contextItem.Tag;
            if(fileinfo.isdir)
            {
                Go(fileinfo.path);
            }
            else
            {
                DownloadFile(fileinfo);
            }
        }

        private void uploadFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PcsFileInfo fileinfo;
            if (contextItem != null)
            {
                fileinfo = (PcsFileInfo)contextItem.Tag;
            }
            else
            {
                fileinfo = new PcsFileInfo();
                fileinfo.path = currentPath;
                fileinfo.isdir = true;
            }
            if (!fileinfo.isdir)
                return;
            if(UploadFile(fileinfo))
            {
                if (string.Equals(fileinfo.path, currentPath, StringComparison.InvariantCultureIgnoreCase))
                    RefreshFileList();
            }
        }

        private void bigIconToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            lvFileList.View = View.LargeIcon;
            bigIconToolStripMenuItem1.Checked = true;
            detailToolStripMenuItem1.Checked = false;
        }

        private void detailToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            lvFileList.View = View.Details;
            bigIconToolStripMenuItem1.Checked = false;
            detailToolStripMenuItem1.Checked = true;
        }

        private void cutToolStripMenuItem2_Click(object sender, EventArgs e)
        {
            if (contextItem == null)
                return;
            PcsFileInfo fileinfo = (PcsFileInfo)contextItem.Tag;
            source = fileinfo;
            isMove = true;
            lblStatus2.Text = "Copied";
        }

        private void copyToolStripMenuItem2_Click(object sender, EventArgs e)
        {
            if (contextItem == null)
                return;
            PcsFileInfo fileinfo = (PcsFileInfo)contextItem.Tag;
            source = fileinfo;
            lblStatus2.Text = "Copied";
        }

        private void deleteToolStripMenuItem2_Click(object sender, EventArgs e)
        {
            if (contextItem == null)
                return;
            PcsFileInfo fileinfo = (PcsFileInfo)contextItem.Tag;
            if (fileinfo.IsEmpty)
                return;
            if(DeleteFile(fileinfo))
            {
                RefreshFileList();
            }
        }

        private void parseToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (source.IsEmpty)
                return;
            PcsFileInfo to;
            if (contextItem != null)
            {
                to = (PcsFileInfo)contextItem.Tag;
                if (!to.isdir)
                    return;
            }
            else
            {
                to = new PcsFileInfo();
                to.path = currentPath;
                to.isdir = true;
            }
            bool succ;
            if (isMove)
                succ = MoveFile(source, to);
            else
                succ = CopyFile(source, to);
            if (succ)
            {
                source = new PcsFileInfo();
                lblStatus2.Text = string.Empty;
                if (string.Equals(source.path, currentPath, StringComparison.InvariantCultureIgnoreCase)
                    || string.Equals(to.path, currentPath, StringComparison.InvariantCultureIgnoreCase))
                    RefreshFileList();
            }
        }

        private void mkdirToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (contextItem != null)
                return;
            if (CreateNewFolder(currentPath))
            {
                RefreshFileList();
            }
        }

        private void metaInformationToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            PcsFileInfo fileinfo = new PcsFileInfo();
            if (contextItem != null)
                fileinfo = (PcsFileInfo)contextItem.Tag;
            else
                fileinfo = GetFileMetaInformation(currentPath);
            if (!fileinfo.IsEmpty)
                ShowFileMetaInformation(fileinfo);
        }

        private void refreshToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            RefreshFileList();
        }

        private void renameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (contextItem == null)
                return;
            PcsFileInfo fileinfo = (PcsFileInfo)contextItem.Tag;
            if (fileinfo.IsEmpty)
                return;
            if (RenameFile(fileinfo))
            {
                RefreshFileList();
            }
        }

        #endregion

        class PcsFileInfoComparer : IComparer<PcsFileInfo>
        {
            public int Compare(PcsFileInfo x, PcsFileInfo y)
            {
                int a = 10, b = 10;
                if(!x.IsEmpty)
                {
                    if (x.isdir)
                        a--;
                }
                if(!y.IsEmpty)
                {
                    if (y.isdir)
                        b--;
                }
                if (a != b)
                    return a - b;
                return string.Compare(x.server_filename, y.server_filename);
            }
        }
    }
}