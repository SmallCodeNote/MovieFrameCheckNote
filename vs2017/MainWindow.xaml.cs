using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.Win32;
using System.Windows.Media;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

using ControlValuesToStringClass;

namespace MovieFrameCheck
{
    public partial class MainWindow : System.Windows.Window
    {
        private const int WM_ENTERSIZEMOVE = 0x0231;

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            HwndSource source = (HwndSource)PresentationSource.FromVisual(this);
            source.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_ENTERSIZEMOVE)
            {
                clearBBox_DisplayedImage();
            }
            return IntPtr.Zero;
        }

        public List<LabelItem> LabelList { get; set; }
        public List<RowData> RowDataList { get; set; }

        private bool suppressionFlag_inLoading = false;

        public MainWindow()
        {
            RowDataList = new List<RowData>();
            suppressionFlag_inLoading = true;
            this.DataContext = this;
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            string inifilepath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "_Param.ini");
            if (File.Exists(inifilepath)) ControlValuesToString.PutValue(this, File.ReadAllText(inifilepath));
            suppressionFlag_inLoading = false;

            UpdateComboBoxDisplay();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            textBox_FeatureInfo.Text = "";
            string inifilepath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "_Param.ini");
            File.WriteAllText(inifilepath, ControlValuesToString.GetString(this));
        }

        private void ZoomComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (ZoomComboBox.SelectedItem is ComboBoxItem selectedItem &&
                    double.TryParse(selectedItem.Tag.ToString(), out double scale))
                {
                    ImageScaleTransform.ScaleX = scale;
                    ImageScaleTransform.ScaleY = scale;
                }

                Dispatcher.InvokeAsync(() => ScrollToImageCenter(), DispatcherPriority.Loaded);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR:{System.Reflection.MethodBase.GetCurrentMethod().Name} {ex.Message} {ex.StackTrace}");

            }
        }

        private void ScrollToImageCenter()
        {
            double imageWidth = image_DisplayedImage.ActualWidth * ImageScaleTransform.ScaleX;
            double imageHeight = image_DisplayedImage.ActualHeight * ImageScaleTransform.ScaleY;

            double viewportWidth = ImageScrollViewer.ViewportWidth;
            double viewportHeight = ImageScrollViewer.ViewportHeight;

            double horizontalOffset = Math.Max(0, (imageWidth - viewportWidth) / 2);
            double verticalOffset = Math.Max(0, (imageHeight - viewportHeight) / 2);

            ImageScrollViewer.ScrollToHorizontalOffset(horizontalOffset);
            ImageScrollViewer.ScrollToVerticalOffset(verticalOffset);
        }

        private void Button_moveImageCenter_Click(object sender, RoutedEventArgs e)
        {
            ScrollToImageCenter();
        }

        private void TabControl_Main_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (suppressionFlag_inLoading) return;

            if ((e.Source as TabControl)?.SelectedItem is TabItem selected &&
                 selected.Header.ToString() == "Main")
            {
                UpdateComboBoxItems();
                UpdateComboBoxDisplay();
            }
        }

        private void UpdateComboBoxItems()
        {
            UpdateLabelList();
            if (DataGrid_FeatureList.Columns[4] is DataGridComboBoxColumn comboColumn)
            {
                comboColumn.ItemsSource = LabelList;
            }
        }

        private void button_dataGrid_CheckAllClear_Click(object sender, RoutedEventArgs e)
        {
            SetSelectedRowsCheck(false);
        }

        private void button_dataGrid_CheckAll_Click(object sender, RoutedEventArgs e)
        {
            SetSelectedRowsCheck(true);
        }

        private void button_dataGrid_RowLabelCopy_Click(object sender, RoutedEventArgs e)
        {

            if (DataGrid_FeatureList.SelectedCells.Count < 1) return;
            int firstLabel = ((RowData)(DataGrid_FeatureList.SelectedCells[0].Item)).Label;


            foreach (var cellInfo in DataGrid_FeatureList.SelectedCells)
            {
                var row = cellInfo.Item as RowData;
                if (row != null) { row.Label = firstLabel; row.Check = true; }
            }

            DataGrid_FeatureList.Items.Refresh();
        }

        bool suppressFlag_DataGrid_FeatureList_CellValueChanged = false;
        string[] RowDataHeader = { "" };
        string[] FeatureHeader = { "" };
        private void button_dataGrid_RowsLoadFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "CSV|*csv";
            if (ofd.ShowDialog() != true) return;

            string[] Lines = File.ReadAllLines(ofd.FileName);

            suppressFlag_DataGrid_FeatureList_CellValueChanged = true;

            RowDataList.Clear();

            RowDataHeader = Lines[0].Split(',');
            string[] cols = Lines[0].Split(',');
            FeatureHeader = cols.Skip(2).Take(cols.Length - 3).ToArray();

            foreach (var Line in Lines)
            {
                cols = Line.Split(',');
                string targetFilenameString = cols[0];
                string frameIndexString = cols[1];
                string labelString = cols[cols.Length - 1];
                string featureString = string.Join(",", cols.Skip(2).Take(cols.Length - 3));

                int labelIndex = -1;
                if (!int.TryParse(labelString, out labelIndex)) { labelIndex = -1; }

                if (int.TryParse(frameIndexString, out int frameindex))
                {
                    RowDataList.Add(new RowData
                    {
                        Check = false,
                        File = targetFilenameString,
                        Frame = frameIndexString,
                        Feature = featureString,
                        Label = labelIndex
                    });

                }
            }
            DataGrid_FeatureList.Items.Refresh();
            suppressFlag_DataGrid_FeatureList_CellValueChanged = false;
        }

        private void button_dataGrid_RowsSaveFile_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "CSV|*.csv";
            sfd.FileName = "LabeledPose.csv";
            sfd.OverwritePrompt = false;

            if (sfd.ShowDialog() != true) return;

            List<string> Lines = new List<string>();

            if (File.Exists(sfd.FileName))
            {
                string[] LinesExist = File.ReadAllLines(sfd.FileName);
                Lines.AddRange(LinesExist);
            }
            else
            {
                Lines.Add(string.Join(",", RowDataHeader));
            }

            foreach (RowData row in RowDataList)
            {
                string filenameString = row.File;
                string frameIndexString = row.Frame;
                string featureString = row.Feature;
                int labelIndex = row.Label;

                try
                {
                    if (row.Check)
                    {
                        string prefix = $"{filenameString},{frameIndexString}";
                        string newLine = $"{prefix},{featureString},{labelIndex}";

                        int existingIndex = Lines.FindIndex(line => line.StartsWith(prefix + ",") || line == prefix);
                        if (existingIndex >= 0)
                        {
                            Lines[existingIndex] = newLine;
                        }
                        else
                        {
                            Lines.Add(newLine);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR:{System.Reflection.MethodBase.GetCurrentMethod().Name} {ex.Message} {ex.StackTrace}");
                }
            }

            string header = Lines.FirstOrDefault();
            var sortedLines = Lines
                .Skip(1)
                .OrderBy(line =>
                {
                    string[] parts = line.Split(',');
                    return parts[0];
                })
                .OrderBy(line =>
                {
                    string[] parts = line.Split(',');
                    if (parts.Length < 1) return int.MaxValue;

                    int frameIndex;
                    return int.TryParse(parts[1], out frameIndex) ? frameIndex : int.MaxValue;
                })
                .ToList();

            sortedLines.Insert(0, header);

            File.WriteAllLines(sfd.FileName, sortedLines);
        }

        private void SetSelectedRowsCheck(bool check)
        {
            var lastSelectedRow = DataGrid_FeatureList.SelectedItem as RowData;

            foreach (var cellInfo in DataGrid_FeatureList.SelectedCells)
            {
                var row = cellInfo.Item as RowData;
                if (row != null) { row.Check = check; }
            }

            DataGrid_FeatureList.Items.Refresh();

            DataGrid_FeatureList.Focus();
            if (lastSelectedRow != null)
            {
                DataGrid_FeatureList.SelectedItem = lastSelectedRow;

                var rowContainer = DataGrid_FeatureList.ItemContainerGenerator.ContainerFromItem(lastSelectedRow) as DataGridRow;
                if (rowContainer != null)
                {
                    rowContainer.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                }
            }
        }

        private void UpdateLabelList()
        {
            if (LabelList == null) LabelList = new List<LabelItem>();
            LabelList.Clear();

            string[] labelNames = textBox_LabelList.Text.Split(
                new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < labelNames.Length; i++)
            {
                LabelList.Add(new LabelItem { LabelId = i, LabelName = labelNames[i] });
            }

            textBox_LabelsView.Text = string.Join("\r\n", LabelList.Select(i => i.Display).ToList());

        }

        private void UpdateComboBoxDisplay()
        {
            if (LabelList == null) LabelList = new List<LabelItem>();
            UpdateLabelList();

            if (RowDataList == null || LabelList.Count < 1) return;

            foreach (var row in RowDataList)
            {
                if (row.Label >= 0 && row.Label < LabelList.Count) { row.Label = row.Label; }
                else { row.Label = -1; }
            }

            var comboColumn = DataGrid_FeatureList.Columns.OfType<DataGridComboBoxColumn>().FirstOrDefault();

            if (comboColumn != null) { comboColumn.ItemsSource = LabelList; }

            DataGrid_FeatureList.Items.Refresh();
        }

        private void frameIndexShift(int shiftValue)
        {
            clearBBox_DisplayedImage();

            if (capture == null) return;

            int targetIndex = frameIndex + shiftValue;
            if (targetIndex < 0) targetIndex = 0;
            if (targetIndex >= (int)slider_frameIndex.Maximum) targetIndex = (int)slider_frameIndex.Maximum - 1;

            slider_frameIndex.Value = targetIndex;

        }

        private void button_LoadMovieFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "MP4|*.mp4";
            ofd.FilterIndex = 0;
            if (Directory.Exists(textBox_workDirectoryPath.Text)) ofd.InitialDirectory = textBox_workDirectoryPath.Text;
            if (ofd.ShowDialog() != true) return;

            OpenMovieFile(ofd.FileName);
        }


        VideoCapture capture;
        string targetFilename = "";
        int frameIndex = 0;

        private void OpenMovieFile(string filePath)
        {
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(() => OpenMovieFile(filePath));
                return;
            }

            if (capture != null)
            {
                capture.Dispose();
                targetFilename = "";
            }

            string ext = System.IO.Path.GetExtension(filePath);
            if (string.Equals(ext, ".mp4", StringComparison.OrdinalIgnoreCase))
            {
                targetFilename = System.IO.Path.GetFileNameWithoutExtension(filePath);
                capture = new VideoCapture(filePath);

                slider_frameIndex.Maximum = capture.FrameCount;
                slider_frameIndex.Value = 0;
                ShowFrame(0);
            }
        }

        private void slider_frameIndex_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            frameIndex = (int)e.NewValue;
            ShowFrame(frameIndex);
        }

        private void ShowFrame(int frameIndex)
        {
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(() => ShowFrame(frameIndex));
                return;
            }

            if (capture == null || !capture.IsOpened()) return;

            capture.Set(VideoCaptureProperties.PosFrames, frameIndex);

            using (var frame = new Mat())
            {
                if (capture.Read(frame) && !frame.Empty())
                {
                    var bitmapSrc = BitmapSourceConverter.ToBitmapSource(frame);
                    bitmapSrc.Freeze();
                    image_DisplayedImage.Source = bitmapSrc;
                }
            }

            if (slider_frameIndex.Value != frameIndex) { slider_frameIndex.Value = frameIndex; }

            this.frameIndex = frameIndex;

            label_framePosition.Content = $"{frameIndex} / {slider_frameIndex.Maximum}";

        }

        private void setTextBox_FeatureInfo(RowData rowData)
        {
            if (rowData != null)
            {
                List<string> Lines = new List<string>();

                string[] element = rowData.Feature.Split(',');

                for (int i = 0; i < element.Length; i++)
                {
                    string featureHeader = "";
                    if (FeatureHeader.Length > i) featureHeader = FeatureHeader[i];
                    Lines.Add(featureHeader + "," + element[i]);
                }

                textBox_FeatureInfo.Text = string.Join("\r\n", Lines);
            }

        }

        private void setImage_FrameImage(RowData rowData, string dataDirectory)
        {
            if (rowData == null) return;

            string filename = rowData.File;
            int newFrameIndex = int.Parse(rowData.Frame);

            if (capture == null || targetFilename != filename)
            {
                string filePath = System.IO.Path.Combine(dataDirectory, filename, filename + "_pose");
                if (System.IO.Path.GetExtension(filePath) != ".mp4") { filePath += ".mp4"; }


                if (!File.Exists(filePath))
                {
                    filePath = System.IO.Path.Combine(dataDirectory, filename);
                    if (System.IO.Path.GetExtension(filePath) != ".mp4") { filePath += ".mp4"; }
                }


                if (File.Exists(filePath)) OpenMovieFile(filePath);
                targetFilename = filename;
            }

            ShowFrame(newFrameIndex);

            setImage_updateBBox(rowData);
        }

        private void setImage_updateBBox(RowData rowData)
        {
            try
            {
                float[] f = rowData.Feature.Split(',').Select(s => float.Parse(s)).ToArray();

                float width = f[2];
                float height = f[3];
                float left = f[0] - width / 2f;
                float top = f[1] - height / 2f;

                clearBBox_DisplayedImage();

                string title = "";
                if (rowData.Label >= 0 && LabelList.Count > rowData.Label) { title = LabelList[rowData.Label].Display; }
                drawBBox_DisplayedImage(top, left, width, height, title, rowData.Check);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR:{System.Reflection.MethodBase.GetCurrentMethod().Name} {ex.Message} {ex.StackTrace}");

            }

        }


        RowData changedRowData = null;
        private void DataGrid_FeatureList_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.Column is DataGridComboBoxColumn && e.EditAction == DataGridEditAction.Commit)
            {
                var row = e.Row.Item as RowData;
                if (row != null)
                {
                    row.Check = true;
                }
            }

        }

        /// <summary>
        /// Update in DataGrid_FeatureList_SelectionChanged
        /// </summary>
        private List<RowData> activeRows = null;
        private string inputBuffer = "";
        private void DataGrid_FeatureList_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if ((e.Key == Key.Down) && activeRows.Count == 1 && activeRows[0].Frame != slider_frameIndex.Value.ToString())
            {
                if (double.TryParse(activeRows[0].Frame, out double targetIndex))
                {
                    slider_frameIndex.Value = targetIndex;
                    setImage_FrameImage(activeRows[0], textBox_workDirectoryPath.Text);
                    e.Handled = true;
                }
            }

            if ((e.Key == Key.Up) && activeRows.Count == 1 && activeRows[0].Frame != slider_frameIndex.Value.ToString())
            {
                var list = DataGrid_FeatureList.ItemsSource as IList<RowData>;
                var matchingItems = list.Where(item => item.Frame == slider_frameIndex.Value.ToString()).ToList();

                if (matchingItems.Count > 0)
                {
                    DataGrid_FeatureList.SelectedItems.Clear();
                    DataGrid_FeatureList.SelectedItems.Add(matchingItems[0]);
                    DataGrid_FeatureList.ScrollIntoView(matchingItems[0]);
                }
                FocusCurrentRow(DataGrid_FeatureList);
                e.Handled = true;
            }


            if (e.Key == Key.Enter)
            {
                for (int ri = 0; ri < activeRows.Count; ri++)
                {
                    if (activeRows[ri] == null) continue;
                    activeRows[ri].Check = !activeRows[ri].Check;
                }

                if (activeRows.Count > 1) { e.Handled = true; }
            }

            int? digit = null;

            if (e.Key >= Key.D0 && e.Key <= Key.D9) digit = e.Key - Key.D0;
            else if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9) digit = e.Key - Key.NumPad0;
            else { inputBuffer = ""; return; }

            int labelValue = -1;

            if (digit.HasValue && activeRows != null)
            {
                inputBuffer += digit.Value.ToString();

                if (int.TryParse(inputBuffer, out labelValue))
                {
                    if (!ContainsLabelId(labelValue) && !ContainsLabelId(digit.Value))
                    {
                        inputBuffer = ""; return;
                    }

                    if (!ContainsLabelId(labelValue) && ContainsLabelId(digit.Value))
                    {
                        labelValue = digit.Value;
                        inputBuffer = digit.Value.ToString();
                    }

                    for (int ri = 0; ri < activeRows.Count; ri++)
                    {
                        if (activeRows[ri] == null) continue;
                        activeRows[ri].Label = labelValue;
                        activeRows[ri].Check = true;
                    }

                    setImage_updateBBox(activeRows[activeRows.Count-1]);
                }

                FocusCurrentRow(DataGrid_FeatureList);
            }

        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (tabItem_Main.IsSelected)
            {
                if (e.Key == Key.Left)
                {
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        frameIndexShift(-5);
                    }
                    else
                    {
                        frameIndexShift(-1);
                    }
                    e.Handled = true;
                }
                if (e.Key == Key.Right)
                {
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        frameIndexShift(5);
                    }
                    else
                    {
                        frameIndexShift(1);
                    }
                    e.Handled = true;
                }
            }
        }

        private void button_SkipBackFewFrames_Click(object sender, RoutedEventArgs e)
        {
            frameIndexShift(-5);
        }

        private void button_PreviousFrame_Click(object sender, RoutedEventArgs e)
        {
            frameIndexShift(-1);
        }

        private void button_NextFrame_Click(object sender, RoutedEventArgs e)
        {
            frameIndexShift(1);
        }

        private void button_SkipForwardFewFrames_Click(object sender, RoutedEventArgs e)
        {
            frameIndexShift(5);
        }

        public bool ContainsLabelId(int targetLabel)
        {
            return LabelList.Any(item => item.LabelId == targetLabel);
        }

        private void FocusCurrentRow(DataGrid dataGrid)
        {

            var cellInfo = dataGrid.SelectedCells.FirstOrDefault();
            if (!cellInfo.Equals(default(DataGridCellInfo)))
            {
                dataGrid.CurrentCell = cellInfo;
                VirtualizationAwareLogic(dataGrid, cellInfo);

                var row = dataGrid.ItemContainerGenerator.ContainerFromItem(cellInfo.Item) as DataGridRow;
                if (row != null)
                {
                    row.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                }
            }
        }

        private void UpdateDataGridRowFocus(DataGrid dataGrid)
        {
            dataGrid.Focus();

            var cellInfo = dataGrid.SelectedCells.FirstOrDefault();

            if (!cellInfo.Equals(default(DataGridCellInfo)))
            {
                VirtualizationAwareLogic(dataGrid, cellInfo);

                var row = dataGrid.ItemContainerGenerator.ContainerFromItem(cellInfo.Item) as DataGridRow;
                if (row != null)
                {
                    row.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                }
            }

        }

        private void VirtualizationAwareLogic(DataGrid dataGrid)
        {
            var cellInfo = dataGrid.SelectedCells.FirstOrDefault();
            VirtualizationAwareLogic(dataGrid, cellInfo);
        }

        private void VirtualizationAwareLogic(DataGrid dataGrid, DataGridCellInfo cellInfo)
        {
            if (!cellInfo.Equals(default(DataGridCellInfo)))
            {
                var item = cellInfo.Item;
                dataGrid.UpdateLayout();  // ... Update ItemContainerGenerator
                dataGrid.ScrollIntoView(item); // ... Add VisualTree (target control move in view)
            }
        }

        RowData LastSelectedRowData = null;

        private void DataGrid_FeatureList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            activeRows = DataGrid_FeatureList.SelectedItems.OfType<RowData>().ToList();

            if (e.AddedItems.Count > 0)
            {
                var lastSelected = e.AddedItems[e.AddedItems.Count - 1] as RowData;
                if (lastSelected != null)
                {
                    LastSelectedRowData = lastSelected;
                    setTextBox_FeatureInfo(lastSelected);

                    setImage_FrameImage(lastSelected, textBox_workDirectoryPath.Text);

                }
                else { LastSelectedRowData = null; }

            }

            if (activeRows.Count <= 0) inputBuffer = "";
        }

        private void button_SortLabeledPicture_Click(object sender, RoutedEventArgs e)
        {
            int RowsCount = RowDataList.Count;

            for (int ri = 0; ri < RowsCount; ri++)
            {
                RowData row = RowDataList[ri];

                if (!row.Check) continue;

                int newFrameIndex = int.Parse(row.Frame);
                string label = row.Label.ToString();


                string filename = row.File;
                string[] poseCols = row.Feature.Split(',');

                float Bbox_Cx = float.Parse(poseCols[0]);
                float Bbox_Cy = float.Parse(poseCols[1]);
                float Bbox_W = float.Parse(poseCols[2]);
                float Bbox_H = float.Parse(poseCols[3]);

                if (capture == null || targetFilename != filename)
                {
                    string filePath = System.IO.Path.Combine(textBox_workDirectoryPath.Text, filename);

                    if (System.IO.Path.GetExtension(filePath) != ".mp4") { filePath += ".mp4"; }

                    if (File.Exists(filePath)) OpenMovieFile(filePath);
                    targetFilename = filename;
                }

                if (capture != null)
                {
                    ShowFrame(newFrameIndex);

                    string topDirectory = textBox_sortDirectoryPath.Text;
                    string newFrameIndexString = newFrameIndex.ToString("00000000");

                    string dirPath = System.IO.Path.Combine(topDirectory, label);
                    string filepath = System.IO.Path.Combine(dirPath, filename + "," + newFrameIndexString + "," + ri.ToString("000") + ".jpg");

                    float Left = Bbox_Cx - Bbox_W / 2f;
                    float Top = Bbox_Cy - Bbox_H / 2f;
                    float Width = Bbox_W;
                    float Height = Bbox_H;
                    int offset = (int)(Width / 3f);


                    saveTrimImage(filepath, (BitmapSource)image_DisplayedImage.Source, Top - offset, Left - offset, (Width + offset * 2f), (Height + offset * 2f));

                }
            }
        }

        void drawBBox_DisplayedImage(float top, float left, float width, float height, string title)
        {
            if (image_DisplayedImage.Source is BitmapSource bitmap)
            {
                double imageWidth = image_DisplayedImage.ActualWidth;
                double imageHeight = image_DisplayedImage.ActualHeight;

                double offsetX = (overlayCanvas.ActualWidth - imageWidth) / 2.0;
                double offsetY = (overlayCanvas.ActualHeight - imageHeight) / 2.0;

                double scaleX = imageWidth / bitmap.PixelWidth;
                double scaleY = imageHeight / bitmap.PixelHeight;

                double rectLeft = left * scaleX + offsetX;
                double rectTop = top * scaleY + offsetY;
                double rectWidth = width * scaleX;
                double rectHeight = height * scaleY;

                System.Windows.Shapes.Rectangle bbox = new System.Windows.Shapes.Rectangle
                {
                    Stroke = System.Windows.Media.Brushes.Red,
                    StrokeThickness = 3,
                    Width = rectWidth,
                    Height = rectHeight
                };

                Canvas.SetLeft(bbox, rectLeft);
                Canvas.SetTop(bbox, rectTop);
                overlayCanvas.Children.Add(bbox);

            }
        }

        void drawBBox_DisplayedImage(float top, float left, float width, float height, string title, bool check)
        {
            if (image_DisplayedImage.Source is BitmapSource bitmap)
            {
                double imageWidth = image_DisplayedImage.ActualWidth;
                double imageHeight = image_DisplayedImage.ActualHeight;

                double offsetX = (overlayCanvas.ActualWidth - imageWidth) / 2.0;
                double offsetY = (overlayCanvas.ActualHeight - imageHeight) / 2.0;

                double scaleX = imageWidth / bitmap.PixelWidth;
                double scaleY = imageHeight / bitmap.PixelHeight;

                double rectLeft = left * scaleX + offsetX;
                double rectTop = top * scaleY + offsetY;
                double rectWidth = width * scaleX;
                double rectHeight = height * scaleY;

                // Draw bounding box
                System.Windows.Shapes.Rectangle bbox = new System.Windows.Shapes.Rectangle
                {
                    Stroke = System.Windows.Media.Brushes.Red,
                    StrokeThickness = 3,
                    Width = rectWidth,
                    Height = rectHeight
                };

                Canvas.SetLeft(bbox, rectLeft);
                Canvas.SetTop(bbox, rectTop);
                overlayCanvas.Children.Add(bbox);

                // Calculate dynamic font size
                double baseFontSize = 12.0;
                double scale = Math.Max(scaleX, scaleY);
                double dynamicFontSize = baseFontSize * scale;

                // Draw title text
                TextBlock titleText = new TextBlock
                {
                    Text = title,
                    Foreground = check ? Brushes.Red : Brushes.Blue,
                    FontSize = dynamicFontSize,
                    FontWeight = FontWeights.Bold,
                    Background = Brushes.White
                };

                Canvas.SetLeft(titleText, rectLeft);
                Canvas.SetTop(titleText, rectTop);
                overlayCanvas.Children.Add(titleText);
            }
        }


        void clearBBox_DisplayedImage()
        {
            overlayCanvas.Children.Clear();
        }

        void saveTrimImage(string savefilepath, BitmapSource src, float top, float left, float width, float height)
        {
            if (top < 0) top = 0f;
            if (left < 0) left = 0f;

            var cropRect = new Int32Rect(
                (int)Math.Floor(left),
                (int)Math.Floor(top),
                Math.Max(0, Math.Min((int)Math.Floor(width), src.PixelWidth - (int)Math.Floor(left))),
                Math.Max(0, Math.Min((int)Math.Floor(height), src.PixelHeight - (int)Math.Floor(top)))
            );

            var croppedBitmap = new CroppedBitmap(src, cropRect);

            var encoder = new JpegBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(croppedBitmap));

            string saveDirectoryPath = System.IO.Path.GetDirectoryName(savefilepath);
            if (!Directory.Exists(saveDirectoryPath)) { Directory.CreateDirectory(saveDirectoryPath); };

            using (var stream = new FileStream(savefilepath, FileMode.Create))
            {
                encoder.Save(stream);
            }
        }


        private void button_xDirectoryPathOpen_Click(object sender, RoutedEventArgs e)
        {
            TextBox textBox = selectTextBoxFromButtonInstance((Button)sender);

            string path = textBox.Text;

            if (Directory.Exists(path))
            {
                Process.Start("explorer.exe", path);
            }
            else
            {
                var dialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();
                dialog.Description = "Select Directory";
                bool result = dialog.ShowDialog() ?? false;

                if (result)
                {
                    textBox.Text = dialog.SelectedPath;
                }
            }
        }

        private void Button_xDirectoryPathClear_Click(object sender, RoutedEventArgs e)
        {
            TextBox textBox = selectTextBoxFromButtonInstance((Button)sender);
            textBox.Text = "";
        }

        private TextBox selectTextBoxFromButtonInstance(Button button)
        {
            if (button == button_sortDirectoryPathClear || button == button_sortDirectoryPathOpen)
            {
                return textBox_sortDirectoryPath;
            }

            return textBox_workDirectoryPath;
        }

        private void button_workDirectoryPathOpen_Click(object sender, RoutedEventArgs e)
        {
            string path = textBox_workDirectoryPath.Text;

            if (Directory.Exists(path))
            {
                Process.Start("explorer.exe", path);
            }
            else
            {
                var dialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();
                dialog.Description = "Select working directory";
                bool result = dialog.ShowDialog() ?? false;

                if (result)
                {
                    textBox_workDirectoryPath.Text = dialog.SelectedPath;
                }
            }
        }

        private void Button_workDirectoryPathClear_Click(object sender, RoutedEventArgs e)
        {
            textBox_workDirectoryPath.Text = "";
        }

        private void DataGrid_FeatureList_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] droppedFiles = (string[])e.Data.GetData(DataFormats.FileDrop);
                var list = DataGrid_FeatureList.ItemsSource as IList<RowData>;
                if (list == null) return;

                DataGrid_FeatureList.SelectedItems.Clear();

                foreach (var fullPath in droppedFiles)
                {
                    string targetFilename = System.IO.Path.GetFileNameWithoutExtension(fullPath);
                    string[] cols = targetFilename.Split(',');
                    if (cols.Length >= 2)
                    {
                        string file = cols[0];
                        if (int.TryParse(cols[1], out int frame))
                        {
                            var matchingItems = list.Where(item => item.File == file && item.Frame == frame.ToString());
                            foreach (var item in matchingItems)
                            {
                                DataGrid_FeatureList.SelectedItems.Add(item);
                                DataGrid_FeatureList.ScrollIntoView(item);
                            }
                        }
                    }
                }

                FocusCurrentRow(DataGrid_FeatureList);
            }
        }

        private void Button_fileListClear_Click(object sender, RoutedEventArgs e)
        {
            textBox_fileList.Text = "";
        }

        private void Button_fileListAdd_Click(object sender, RoutedEventArgs e)
        {
            string path = textBox_workDirectoryPath.Text;

            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "MP4|*.mp4";
            ofd.FilterIndex = 0;
            if (Directory.Exists(textBox_workDirectoryPath.Text)) ofd.InitialDirectory = textBox_workDirectoryPath.Text;
            if (ofd.ShowDialog() != true) return;

            List<string> fileList = new List<string>(textBox_fileList.Text.Replace("\r\n", "\n").Trim('\n').Split('\n'));
            fileList.AddRange(ofd.FileNames);
            textBox_fileList.Text = string.Join("\r\n", fileList.Distinct());

        }

        private void Image_DisplayedImage_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (image_DisplayedImage.Source is BitmapSource bitmapSource)
            {

                if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                {
                    Clipboard.SetImage(bitmapSource);
                }
                else
                {
                    var dialog = new Microsoft.Win32.SaveFileDialog
                    {
                        Filter = "JPEG (*.jpg)|*.jpg",
                        DefaultExt = ".jpg",
                        FileName = $"{frameIndex},{targetFilename}"
                    };

                    if (dialog.ShowDialog() == true)
                    {
                        using (var stream = new FileStream(dialog.FileName, FileMode.Create))
                        {
                            var encoder = new JpegBitmapEncoder();
                            encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
                            encoder.Save(stream);
                        }
                    }
                }
            }
        }

        private void Button_ImageSendClipboard_Click(object sender, RoutedEventArgs e)
        {
            if (image_DisplayedImage.Source is BitmapSource bitmapSource)
            {
                Clipboard.SetImage(bitmapSource);
            }
        }

        private void Button_ImageSaveFile_Click(object sender, RoutedEventArgs e)
        {
            if (image_DisplayedImage.Source is BitmapSource bitmapSource)
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "JPEG (*.jpg)|*.jpg",
                    DefaultExt = ".jpg",
                    FileName = $"{frameIndex},{targetFilename}"
                };

                if (dialog.ShowDialog() == true)
                {
                    using (var stream = new FileStream(dialog.FileName, FileMode.Create))
                    {
                        var encoder = new JpegBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
                        encoder.Save(stream);
                    }
                }
            }
        }
    }

    public class LabelItem
    {
        public int LabelId { get; set; }
        public string LabelName { get; set; }
        public string Display => LabelId < 0 ? "" : $"{LabelId}: {LabelName}";
    }

    public class RowData : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public bool _check { get; set; }
        public bool Check
        {
            get => _check;
            set
            {
                if (_check != value)
                {
                    _check = value;
                    OnPropertyChanged();
                }
            }
        }

        public string File { get; set; }
        public string Frame { get; set; }
        public string Feature { get; set; }

        private int _label;
        public int Label
        {
            get => _label;
            set
            {
                if (_label != value)
                {
                    _label = value;
                    OnPropertyChanged();
                }
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

    }

}
