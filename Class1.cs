using PEPlugin;
using PEPlugin.Pmx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace PMXAssetRelinker
{
    public class Class1 : PEPluginClass
    {
        Dictionary<string, string> copied;
        string modelDir;
        string outputDir;
        string assetsDir;
        string subFolderName = "assets";

        public Class1() : base()
        {
            m_option = new PEPluginOption(false, true, "テクスチャ一括書き出し");
        }

        public override void Run(IPERunArgs args)
        {
            base.Run(args);

            try
            {
                IPEConnector connect = args.Host.Connector;
                IPXPmx pmx = connect.Pmx.GetCurrentState();
                if (pmx == null || string.IsNullOrEmpty(pmx.FilePath))
                {
                    MessageBox.Show("モデルが読み込まれていません。");
                    return;
                }
                string modelPath = pmx.FilePath;
                modelDir = Path.GetDirectoryName(modelPath);
                if (string.IsNullOrEmpty(modelDir))
                {
                    throw new InvalidOperationException("モデルのパスの取得に失敗しました。");
                }
                // 正規化して末尾に区切り文字を扱いやすくする
                try
                {
                    modelDir = Path.GetFullPath(modelDir);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("モデルのパスの正規化に失敗しました。", ex);
                }
                // ===== UI表示 =====
                if (!ShowForm())
                    return;

                assetsDir = Path.Combine(outputDir, subFolderName);

                if (Directory.Exists(assetsDir) && Directory.EnumerateFiles(assetsDir, "*", SearchOption.AllDirectories).Any())
                {
                    var result = MessageBox.Show(
                        $"{subFolderName}フォルダ内に既にファイルがあります。上書きしますか？",
                        "確認",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning
                    );

                    if (result != DialogResult.Yes)
                        return;
                }

                Directory.CreateDirectory(assetsDir);

                copied = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var mat in pmx.Material)
                {
                    mat.Tex = ProcessPath(mat.Tex);
                    mat.Sphere = ProcessPath(mat.Sphere);
                    mat.Toon = ProcessPath(mat.Toon);
                }

                args.Host.Connector.Pmx.Update(pmx);
                args.Host.Connector.Form.UpdateList(PEPlugin.Pmd.UpdateObject.Header);

                MessageBox.Show(
                    "書き出し完了！" + Environment.NewLine + Environment.NewLine +
                    "※　モデルは変更されています。" + Environment.NewLine +
                    "　　必ず出力フォルダに別名で保存してください。",
                    "完了",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "エラー", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        private bool ShowForm()
        {
            using (var form = new Form())
            {
                form.Text = "書き出し設定";
                form.Width = 600;
                form.Height = 180;
                form.StartPosition = FormStartPosition.CenterParent;

            System.Windows.Forms.Label lblDir = new System.Windows.Forms.Label() { Text = "出力フォルダ", Left = 10, Top = 20, Width = 100 };
            TextBox txtDir = new TextBox() { Left = 110, Top = 20, Width = 400, Text = outputDir };
            Button btnBrowse = new Button() { Text = "...", Left = 520, Top = 18, Width = 40 };

            System.Windows.Forms.Label lblSub = new System.Windows.Forms.Label() { Text = "サブフォルダ名", Left = 10, Top = 60, Width = 100 };
            TextBox txtSub = new TextBox() { Left = 110, Top = 60, Width = 400, Text = subFolderName };

            Button btnOK = new Button() { Text = "実行", Left = 250, Top = 100, Width = 80 };
            btnOK.DialogResult = DialogResult.OK;

            txtDir.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            btnBrowse.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            txtSub.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            // ===== フォルダ選択 =====
            btnBrowse.Click += (s, e) =>
            {
                using (var fbd = new FolderBrowserDialog())
                {
                    if (fbd.ShowDialog() == DialogResult.OK)
                        txtDir.Text = fbd.SelectedPath;
                }
            };

            // ===== ドラッグ＆ドロップ対応 =====
            txtDir.AllowDrop = true;

            txtDir.DragEnter += (s, e) =>
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                    e.Effect = DragDropEffects.Copy;
            };

            txtDir.DragDrop += (s, e) =>
            {
                var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (paths.Length > 0 && Directory.Exists(paths[0]))
                {
                    txtDir.Text = paths[0];
                }
            };

                form.Controls.Add(lblDir);
                form.Controls.Add(txtDir);
                form.Controls.Add(btnBrowse);
                form.Controls.Add(lblSub);
                form.Controls.Add(txtSub);
                form.Controls.Add(btnOK);

                form.AcceptButton = btnOK;

                if (form.ShowDialog() != DialogResult.OK)
                    return false;

            if (string.IsNullOrWhiteSpace(txtDir.Text))
            {
                MessageBox.Show("出力フォルダを指定してください");
                return false;
            }

                outputDir = txtDir.Text;
                subFolderName = string.IsNullOrWhiteSpace(txtSub.Text) ? "assets" : txtSub.Text;

                return true;
            }
        }

        private string ProcessPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;

            // 絶対パス化
            string fullPath = path;
            try
            {
                if (!Path.IsPathRooted(path))
                    fullPath = Path.GetFullPath(Path.Combine(modelDir, path));
                else
                    fullPath = Path.GetFullPath(path);
            }
            catch
            {
                // Path.GetFullPath が失敗したら元の値を使う
                fullPath = Path.Combine(modelDir, path);
            }

            if (!File.Exists(fullPath))
            {
                var exeDir = AppDomain.CurrentDomain.BaseDirectory;
                var toonPath = Path.Combine(exeDir, "toon", path);

                if (File.Exists(toonPath))
                    fullPath = Path.GetFullPath(toonPath);
                else
                    throw new FileNotFoundException($"ファイルが見つかりません: {path}", fullPath);
            }

            // copied のキーは正規化したフルパスを使う
            string key = fullPath;

            if (!copied.ContainsKey(key))
            {
                string relativePath;

                // modelDir の末尾に区切り文字を追加して部分一致の誤判定を避ける
                var modelDirForCompare = modelDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

                if (fullPath.StartsWith(modelDirForCompare, StringComparison.OrdinalIgnoreCase))
                {
                    relativePath = fullPath.Substring(modelDirForCompare.Length).TrimStart('\\', '/');
                }
                else
                {
                    relativePath = Path.GetFileName(fullPath);
                }

                string destPath = Path.Combine(assetsDir, relativePath);
                var destDir = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(destDir))
                    Directory.CreateDirectory(destDir);

                try
                {
                    File.Copy(fullPath, destPath, true);
                }
                catch (Exception ex)
                {
                    throw new IOException($"ファイルのコピーに失敗しました: {fullPath}", ex);
                }

                string pmxPath = Path.Combine(subFolderName, relativePath).Replace("\\", "/");

                copied[key] = pmxPath;
            }

            return copied[key];
        }
    }
}
