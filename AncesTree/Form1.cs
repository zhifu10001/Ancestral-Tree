﻿using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using AncesTree.TreeLayout;
using AncesTree.TreeModel;
using DrawAnce;
using GEDWrap;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

// ReSharper disable InconsistentNaming


namespace AncesTree
{
    public partial class Form1 : Form
    {
        readonly List<CmbItem> _cmbItems = new List<CmbItem>();
        protected MruStripMenu mnuMRU;

        public Form1()
        {
            InitializeComponent();

            mnuMRU = new MruStripMenuInline(fileToolStripMenuItem, recentFilesToolStripMenuItem, OnMRU);
            mnuMRU.MaxEntries = 7;

            LoadSettings(); // NOTE: must go after mnuMRU init

            treePanel1.OnNodeClick += TreePanel1_OnNodeClick;
            treePanel1.OnNodeHover += TreePanel1_OnNodeHover;
        }

        private void TreePanel1_OnNodeHover(object sender, ITreeData node)
        {
            var pnode = node as PersonNode;
            if (pnode == null)
            {
                toolTip1.Hide(this);
            }
            else
            {
                toolTip1.Show(pnode.Text, this, PointToClient(Control.MousePosition));
            }
        }

        private void TreePanel1_OnNodeClick(object sender, ITreeData node)
        {
            var pnode = node as PersonNode;
            if (pnode == null)
                return;
            for (int i = 0; i < _cmbItems.Count; i++)
            {
                if (_cmbItems[i].Value == pnode.Who)
                {
                    personSel.SelectedIndex = i;
                    return;
                }
            }
        }

        private void OnMRU(int number, string filename)
        {
            if (!File.Exists(filename))
            {
                mnuMRU.RemoveFile(number);
                MessageBox.Show("The file no longer exists: " + filename);
                return;
            }
            LastFile = filename;
            mnuMRU.SetFirstFile(number);
            ProcessGED(filename);
        }

        #region Settings
        private DASettings _mysettings;

        private List<string> _fileHistory = new List<string>();

        private string LastFile
        {
            get
            {
                if (_fileHistory == null || _fileHistory.Count < 1)
                    return null;
                return _fileHistory[0]; // First entry is the most recent
            }
            set
            {
                // Make sure to wipe any older instance
                _fileHistory.Remove(value);
                _fileHistory.Insert(0, value); // First entry is the most recent
            }
        }

        private void LoadSettings()
        {
            _mysettings = DASettings.Load();

            // No existing settings. Use default.
            if (_mysettings.Fake)
            {
                StartPosition = FormStartPosition.CenterScreen;
            }
            else
            {
                // restore windows position
                StartPosition = FormStartPosition.Manual;
                Top = _mysettings.WinTop;
                Left = _mysettings.WinLeft;
                Height = _mysettings.WinHigh;
                Width = _mysettings.WinWide;
                _fileHistory = _mysettings.PathHistory ?? new List<string>();
                _fileHistory.Remove(null);
                mnuMRU.SetFiles(_fileHistory.ToArray());

                LastFile = _mysettings.LastPath;
            }
        }

        private void SaveSettings()
        {
            // TODO check minimized
            var bounds = DesktopBounds;
            _mysettings.WinTop = Location.Y;
            _mysettings.WinLeft = Location.X;
            _mysettings.WinHigh = bounds.Height;
            _mysettings.WinWide = bounds.Width;
            _mysettings.Fake = false;
            _mysettings.LastPath = LastFile;
            _mysettings.PathHistory = mnuMRU.GetFiles().ToList();
            _mysettings.Save();
        }
        #endregion

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            SaveSettings();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void TreePerson(Person val)
        {
            var config = TreeConfiguration.LoadConfig();
            var tree = TreeBuild.BuildTree(treePanel1, config, val);

            // create the NodeExtentProvider for TextInBox nodes
            var nodeExtentProvider = new NodeExtents();

            // create the layout
            var treeLayout = new TreeLayout<ITreeData>(tree, nodeExtentProvider, config);
            treePanel1.Boxen = treeLayout;
        }

        private void personSel_SelectedIndexChanged(object sender, EventArgs e)
        {
            var val = personSel.SelectedValue as Person;
            if (val == null)
                return;
            TreePerson(val);
        }

        private void ProcessGED(string gedPath)
        {
            Text = gedPath;
            Application.DoEvents(); // Cycle events so image updates in case GED load/process takes a while
            LoadGed();
        }

        private void loadGEDCOMToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var ofd = new OpenFileDialog();
            ofd.Multiselect = false;
            ofd.Filter = "GEDCOM files|*.ged;*.GED";
            ofd.FilterIndex = 1;
            ofd.DefaultExt = "ged";
            ofd.CheckFileExists = true;
            if (DialogResult.OK != ofd.ShowDialog(this))
            {
                return;
            }
            mnuMRU.AddFile(ofd.FileName);
            LastFile = ofd.FileName; // TODO invalid ged file
            ProcessGED(ofd.FileName);
        }

        private Forest gedtrees;

        private class CmbItem
        {
            public string Text { get; set; }
            public Person Value { get; set; }
        }

        void LoadGed()
        {
            gedtrees = new Forest();
            gedtrees.LoadGEDCOM(LastFile);

            personSel.SelectedIndexChanged -= personSel_SelectedIndexChanged;
            personSel.Enabled = false;
            personSel.BeginUpdate();
            personSel.DataSource = null;
            _cmbItems.Clear();

            HashSet<string> comboNames = new HashSet<string>();
            Dictionary<string, Person> comboPersons = new Dictionary<string, Person>();
            foreach (var indiId in gedtrees.AllIndiIds)
            {
                Person p = gedtrees.PersonById(indiId);

                var text = string.Format("{0},{1} [{2}]", p.Surname, p.Given, indiId);
                comboNames.Add(text);
                comboPersons.Add(text, p);
            }

            var nameSort = comboNames.ToArray();
            Array.Sort(nameSort);
            foreach (var s in nameSort)
            {
                _cmbItems.Add(new CmbItem {Text=s,Value=comboPersons[s]});
            }
            
            personSel.DisplayMember = "Text";
            personSel.ValueMember = "Value";
            personSel.DataSource = _cmbItems;
            personSel.SelectedIndex = -1; // force SelectedIndexChanged to happen below
            personSel.EndUpdate();
            personSel.Enabled = true;
            personSel.SelectedIndexChanged += personSel_SelectedIndexChanged;
            personSel.SelectedIndex = 0;
        }

        private void btnZoomIn_Click(object sender, EventArgs e)
        {
            treePanel1.Zoom = treePanel1.Zoom + 0.1f;
        }

        private void btnZoomOut_Click(object sender, EventArgs e)
        {
            treePanel1.Zoom = treePanel1.Zoom - 0.1f;
        }

        private void btn100Percent_Click(object sender, EventArgs e)
        {
            treePanel1.Zoom = 1.0f;
        }

        private void btnToImage_Click(object sender, EventArgs e)
        {
            // save to image

            using (Bitmap b = new Bitmap(treePanel1.Width, treePanel1.Height))
            {
                using (Graphics g = Graphics.FromImage(b))
                {
                    treePanel1.drawTree(g);
                }
                b.Save(@"e:\ancestree.png", ImageFormat.Png);
            }
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (!e.Control)
                return;
            if (e.KeyCode == Keys.Oemplus)
            {
                btnZoomIn_Click(null, null);
            }
            else if (e.KeyCode == Keys.OemMinus)
            {
                btnZoomOut_Click(null, null);
            }
        }

        private void btnConfig_Click(object sender, EventArgs e)
        {
            Settings dlg = new Settings();
            dlg.Owner = this;
            dlg.ShowDialog();
            treePanel1.Invalidate();
        }
    }
}
