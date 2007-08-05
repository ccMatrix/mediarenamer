using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.IO;
using System.ServiceProcess;
using MediaRenamer.Common;

namespace MediaRenamer
{
    public partial class mainForm : Form
    {
        public static Form instance = null;

        public mainForm()
        {
            InitializeComponent();
            mainForm.instance = this;
        }

        private void mainForm_Load(object sender, EventArgs e)
        {
            Text = Application.ProductName + " v" + Application.ProductVersion;

            addWatchType.Items.Add(WatchFolderEntryType.MOVIES);
            addWatchType.Items.Add(WatchFolderEntryType.SERIES);


            notifyIcon.Visible = Settings.GetValueAsBool(SettingKeys.SysTrayIcon);
            optionSysTray.Checked = Settings.GetValueAsBool(SettingKeys.SysTrayIcon);

            option_movieFormat.Text = Settings.GetValueAsString(SettingKeys.MovieFormat);
            option_seriesFormat.Text = Settings.GetValueAsString(SettingKeys.SeriesFormat);

            Object[] items = null;
            items = Settings.GetValueAsArray(SettingKeys.MoviePaths);
            foreach (Object o in items)
            {
                movieScanPath.Items.Add(o);
            }
            items = Settings.GetValueAsArray(SettingKeys.SeriesPaths);
            foreach (Object o in items)
            {
                seriesScanPath.Items.Add(o);
            }

            items = Settings.GetValueAsArray(SettingKeys.WatchedFolders);
            foreach (Object o in items)
            {
                WatchedFolderEntry wfe = (WatchedFolderEntry)o;
                watchedFolders.Items.Add(wfe);
            }

            String uiLang = Settings.GetValueAsString(SettingKeys.UILanguage);
            i18nLang ui18nLang = new i18nLang("en", "english");
            option_langUI.Items.Add(ui18nLang);
            option_langUI.SelectedItem = ui18nLang;

        }

        private void mainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (Settings.GetValueAsBool(SettingKeys.SysTrayIcon))
            {
                if (e.CloseReason == CloseReason.UserClosing)
                {
                    e.Cancel = true;
                    this.WindowState = FormWindowState.Minimized;
                    this.ShowInTaskbar = false;
                }
            }
            if (!e.Cancel)
            {
                saveWatchedFolders();
            }
        }

        private void saveWatchedFolders()
        {
            Object[] folders = new Object[watchedFolders.Items.Count];
            for (int i = 0; i < watchedFolders.Items.Count; i++)
            {
                folders[i] = watchedFolders.Items[i];
            }
            Settings.SetValue(SettingKeys.WatchedFolders, folders);
        }

        delegate void insertLogCallback(string text);
        public void insertLog(string text)
        {
            // InvokeRequired required compares the thread ID of the
            // calling thread to the thread ID of the creating thread.
            // If these threads are different, it returns true.
            if (infoLog.InvokeRequired)
            {
                insertLogCallback d = new insertLogCallback(insertLog);
                this.Invoke(d, new object[] { text });
            }
            else
            {
                infoLog.Items.Insert(0, text);
                infoLog.Update();
            }
        }

        private void infoLog_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (infoLog.SelectedItem != null)
            {
                toolTip.SetToolTip(infoLog, infoLog.SelectedItem.ToString());
            }
        }

        private void contextProposals_Opening(object sender, CancelEventArgs e)
        {
            ContextMenuStrip cms = (sender as ContextMenuStrip);
            if (option_seriesFormat.Focused)
            {
                cms.Items.Clear();
                cms.Items.Add("<series>");
                cms.Items.Add("<season>");
                cms.Items.Add("<season2>");
                cms.Items.Add("<episode>");
                cms.Items.Add("<episode2>");
                cms.Items.Add("<title: >");
                cms.Items.Add("<title>");
            }
            if (option_movieFormat.Focused)
            {
                cms.Items.Clear();
                cms.Items.Add("<moviename>");
                cms.Items.Add("<year: >");
                cms.Items.Add("<year>");
                cms.Items.Add("<disk: >");
                cms.Items.Add("<disk>");
                cms.Items.Add("<lang: >");
                cms.Items.Add("<lang>");
            }
        }

        private void contextProposals_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            ContextMenuStrip cms = (sender as ContextMenuStrip);
            if (option_seriesFormat.Focused)
            {
                option_seriesFormat.SelectedText = e.ClickedItem.Text;
            }
            if (option_movieFormat.Focused)
            {
                option_movieFormat.SelectedText = e.ClickedItem.Text;
            }
        }

        private void tabOptions_Click(object sender, EventArgs e)
        {

        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            option_tvSourceEPW.Select();
        }

        #region WatchFolder Methods

        private void watchAddTest_Click(object sender, EventArgs e)
        {
            if (!Directory.Exists(addWatchPath.Text))
            {
                MessageBox.Show(i18n.t("folder_missing"));
                return;
            }
            if (addWatchType.SelectedIndex == -1)
            {
                MessageBox.Show(i18n.t("folder_notype"));
            }
            WatchedFolderEntry watchedFolder = new WatchedFolderEntry();
            watchedFolder.watchPath = addWatchPath.Text;
            watchedFolder.watchType = (WatchFolderEntryType)addWatchType.SelectedItem;
            watchedFolder.lastChanged = DateTime.MinValue;
            watchedFolder.runThread();

            watchedFolders.Items.Add(watchedFolder);

            addWatchPath.Clear();
            addWatchType.SelectedIndex = -1;

            saveWatchedFolders();
            reloadService();
        }

        private void watchedFolders_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index > watchedFolders.Items.Count)
                return;
            try
            {
                Stream s = null;

                // retrieve information for watched folder
                WatchedFolderEntry watchedFolder = (watchedFolders.Items[e.Index] as WatchedFolderEntry);
                e.DrawBackground();
                Brush b = new SolidBrush(e.ForeColor);
                // get type of watched folder
                String iconName = "movie";
                switch (watchedFolder.watchType)
                {
                    case WatchFolderEntryType.MOVIES:
                        iconName = "movie";
                        break;
                    case WatchFolderEntryType.SERIES:
                        iconName = "television";
                        break;
                }

                // draw type icon
                s = this.GetType().Assembly.GetManifestResourceStream("MediaRenamer.Resources." + iconName + ".png");
                Bitmap folderIcon = new Bitmap(s);
                s.Close();
                e.Graphics.DrawImage(folderIcon, e.Bounds.X + 2, e.Bounds.Y + 2, 16, 16);
                folderIcon.Dispose();

                // draw path of folder
                e.Graphics.DrawString(watchedFolder.watchPath, e.Font, b, e.Bounds.X + 20, e.Bounds.Y + 2);

                // draw last change date
                String lastChanged = "";
                if (watchedFolder.lastChanged != DateTime.MinValue)
                {
                    lastChanged = watchedFolder.lastChanged.ToString();
                }
                else
                {
                    lastChanged = i18n.t("watched_never");
                }
                e.Graphics.DrawString(lastChanged, e.Font, b, e.Bounds.X + 20, e.Bounds.Y + 17);

                // draw thread icon
                String threadGraphic = "";
                if (watchedFolder.threadRunning)
                {
                    threadGraphic = "thread_running";
                }
                else
                {
                    threadGraphic = "thread_stopped";
                }
                s = this.GetType().Assembly.GetManifestResourceStream("MediaRenamer.Resources." + threadGraphic + ".png");
                Bitmap threadIcon = new Bitmap(s);
                s.Close();
                e.Graphics.DrawImage(threadIcon, e.Bounds.Width - 18, e.Bounds.Y + 2, 16, 16);
                threadIcon.Dispose();

                e.DrawFocusRectangle();
            }
            catch (Exception E)
            {
                Log.Add("Drawing Error:" + E.Message);
            }
        }

        private void watchedFolders_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (watchedFolders.SelectedItems.Count > 0)
            {
                WatchedFolderEntry selectedFolder = (WatchedFolderEntry)watchedFolders.SelectedItem;
                watchThreadRun.Enabled = !selectedFolder.threadRunning;
                watchThreadStop.Enabled = selectedFolder.threadRunning;
            }
            else
            {
                watchThreadRun.Enabled = false;
                watchThreadStop.Enabled = false;
            }
        }

        private void watchThreadRun_Click(object sender, EventArgs e)
        {
            if (watchedFolders.SelectedItems.Count > 0)
            {
                WatchedFolderEntry selectedFolder = (WatchedFolderEntry)watchedFolders.SelectedItem;
                selectedFolder.runThread();
                watchedFolders.SelectedItems.Clear();
            }
            reloadService();
        }

        private void watchThreadStop_Click(object sender, EventArgs e)
        {
            if (watchedFolders.SelectedItems.Count > 0)
            {
                WatchedFolderEntry selectedFolder = (WatchedFolderEntry)watchedFolders.SelectedItem;
                selectedFolder.stopThread();
                watchedFolders.SelectedItems.Clear();
            }
            reloadService();
        }

        private void reloadService()
        {
            ServiceController control = new ServiceController("MediaRenamerService");
            control.Stop();
            control.Start();
        }

        #endregion

        private bool validPath(String path)
        {
            if (path == String.Empty)
            {
                MessageBox.Show("Please select a folder!", Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            if (!Directory.Exists(path))
            {
                MessageBox.Show("The selected folder does not exist!\nPlease choose a different one.", Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            return true;
        }

        private void btnMovieScan_Click(object sender, EventArgs e)
        {
            if (!validPath(movieScanPath.Text)) return;
            storeMoviePath(movieScanPath.Text);

            scanMovieList.Items.Clear();
            Cursor = Cursors.WaitCursor;

            MovieRenamer.Parser mparse = new MovieRenamer.Parser(movieScanPath.Text);
            mparse.ScanProgress += new ScanProgressHandler(movie_ScanProgress);
            mparse.ListMovie += new ListMovieHandler(movie_ListMovie);
            mparse.startScan();

            Cursor = Cursors.Default;
        }

        private void storeMoviePath(string p)
        {
            if (movieScanPath.Items.IndexOf(p) == -1)
            {
                movieScanPath.Items.Insert(0, p);
                Settings.SetValue(SettingKeys.MoviePaths, movieScanPath.Items);
            }
        }

        void movie_ListMovie(MovieRenamer.Movie m)
        {
            scanMovieList.Items.Add(m);
        }

        void movie_ScanProgress(int pos, int max)
        {
            if (scanMovieProgressbar.Maximum == 0)
            {
                Log.Add(i18n.t("scan_count", max));
            }
            if (pos == 0 && max == 0)
            {
                Log.Add(i18n.t("scan_complete"));
            }
            scanMovieProgressbar.Maximum = max;
            scanMovieProgressbar.Value = pos;
        }

        private void btnSeriesScan_Click(object sender, EventArgs e)
        {
            if (!validPath(seriesScanPath.Text)) return;
            storeSeriesPath(seriesScanPath.Text);

            scanSeriesList.Items.Clear();
            Cursor = Cursors.WaitCursor;

            TVShowRenamer.Parser tparse = new TVShowRenamer.Parser(seriesScanPath.Text);
            tparse.ScanProgress += new ScanProgressHandler(series_ScanProgress);
            tparse.ListEpisode += new ListEpisodeHandler(series_ListEpisode);
            tparse.startScan();

            Cursor = Cursors.Default;
        }

        private void storeSeriesPath(string p)
        {
            if (seriesScanPath.Items.IndexOf(p) == -1)
            {
                seriesScanPath.Items.Insert(0, p);
                Settings.SetValue(SettingKeys.SeriesPaths, seriesScanPath.Items);
            }
        }

        void series_ListEpisode(TVShowRenamer.Episode ep)
        {
            scanSeriesList.Items.Add(ep);
        }

        void series_ScanProgress(int pos, int max)
        {
            if (scanSeriesProgressbar.Maximum == 0)
            {
                Log.Add(i18n.t("scan_count", max));
            }
            if (pos == 0 && max == 0)
            {
                Log.Add(i18n.t("scan_complete"));
            }
            scanSeriesProgressbar.Maximum = max;
            scanSeriesProgressbar.Value = pos;
        }

        private void contextOptionRename_Click(object sender, EventArgs e)
        {
            if (tabControl.SelectedTab == tabSeries)
            {
                renameSelectedSeries();
            }
            if (tabControl.SelectedTab == tabMovies)
            {
                renameSelectedMovies();
            }
        }

        private void contextRename_Opening(object sender, CancelEventArgs e)
        {
            if (tabControl.SelectedTab == tabSeries)
            {
                contextOptionRename.Enabled = (scanSeriesList.SelectedItems.Count > 0);
            }
            if (tabControl.SelectedTab == tabMovies)
            {
                contextOptionRename.Enabled = (scanMovieList.SelectedItems.Count > 0);
            }

        }

        private void renameSelectedMovies()
        {
            if (scanMovieList.SelectedItems.Count > 0)
            {
                MovieRenamer.Movie m;
                for (int i = scanMovieList.SelectedItems.Count - 1; i >= 0; i--)
                {
                    m = (MovieRenamer.Movie)scanMovieList.SelectedItems[i];
                    m.renameMovie();
                    scanMovieList.Items.Remove(m);
                }
            }
        }

        private void renameSelectedSeries()
        {
            if (scanSeriesList.SelectedItems.Count > 0)
            {
                TVShowRenamer.Episode ep;
                for (int i = scanSeriesList.SelectedItems.Count - 1; i >= 0; i--)
                {
                    ep = (TVShowRenamer.Episode)scanSeriesList.SelectedItems[i];
                    ep.renameEpisode();
                    scanSeriesList.Items.Remove(ep);
                }
            }
        }

        private void scanMovieList_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Return || e.KeyCode == Keys.Enter)
            {
                renameSelectedMovies();
            }
        }

        private void scanSeriesList_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Return || e.KeyCode == Keys.Enter)
            {
                renameSelectedSeries();
            }
        }

        private void option_movieFormat_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                if (!option_movieFormat.Focused)
                    option_movieFormat.Focus();
            }
        }

        private void option_seriesFormat_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                if (!option_seriesFormat.Focused)
                    option_seriesFormat.Focus();
            }
        }


        private void sysTrayExit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void notifyIcon_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                this.WindowState = FormWindowState.Normal;
                this.ShowInTaskbar = true;
            }
        }

        private void notifyIcon_MouseDoubleClick(object sender, MouseEventArgs e)
        {

        }

        private void sysTrayOpen_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Normal;
        }

        private void optionSysTray_CheckedChanged(object sender, EventArgs e)
        {
            Settings.SetValue(SettingKeys.SysTrayIcon, optionSysTray.Checked);
            notifyIcon.Visible = optionSysTray.Checked;
        }

        private void option_seriesFormat_Leave(object sender, EventArgs e)
        {
            Settings.SetValue(SettingKeys.SeriesFormat, option_seriesFormat.Text);
        }

        private void option_movieFormat_Leave(object sender, EventArgs e)
        {
            Settings.SetValue(SettingKeys.MovieFormat, option_movieFormat.Text);
        }

        private void seriesScanPath_TextUpdate(object sender, EventArgs e)
        {
            btnSeriesScan.Enabled = Directory.Exists(seriesScanPath.Text);
        }

        private void movieScanPath_TextUpdate(object sender, EventArgs e)
        {
            btnMovieScan.Enabled = Directory.Exists(movieScanPath.Text);
        }

        private void addWatchFolder_Changed(object sender, EventArgs e)
        {
            addWatchFolder.Enabled = (Directory.Exists(addWatchPath.Text) && (addWatchType.SelectedIndex > -1));
        }
    }
}