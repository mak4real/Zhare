using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Microsoft.Win32;

namespace Zhare
{
    public class DiscoveredDevice
    {
        public string DeviceName { get; set; }
        public string DeviceType { get; set; }
        public string IpAddress { get; set; }
        public int Port { get; set; }
        public DateTime LastSeen { get; set; }

        public override string ToString()
        {
            return string.Format("{0} ({1})", DeviceName, IpAddress);
        }
    }

    public class RemoteFileInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public long Size { get; set; }

        public string FormattedSize
        {
            get
            {
                if (Size <= 0) return "0 B";
                string[] units = new string[] { "B", "KB", "MB", "GB" };
                int digitGroups = (int)(Math.Log10(Size) / Math.Log10(1024));
                if (digitGroups >= units.Length) digitGroups = units.Length - 1;
                double val = Size / Math.Pow(1024, digitGroups);
                return string.Format("{0:0.##} {1}", val, units[digitGroups]);
            }
        }
    }

    public class Program
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_CAPTION_COLOR = 35;

        private static Window mainWindow;

        // Header controls
        private static Border deviceCapsule;
        private static DropShadowEffect deviceGlow;
        private static System.Windows.Shapes.Ellipse statusDot;
        private static TextBlock statusDevName;
        private static TextBlock statusDevIp;
        private static TextBlock statusDropdownArrow;
        private static TextBlock networkInfoText;

        // Quick connect & utility bar
        private static TextBox txtDirectIp;
        private static Button btnDirectConnect;
        private static Button btnRescan;
        private static Button btnOpenDownloads;

        // Segmented navigation
        private static Border navPillSend;
        private static Border navPillReceive;
        private static TextBlock navTextSend;
        private static TextBlock navTextReceive;
        private static Grid viewSend;
        private static Grid viewReceive;

        // Send View controls
        private static Border dropZone;
        private static DropShadowEffect dropZoneGlow;
        private static StackPanel fileListStack;
        private static TextBlock queueCountBadge;
        private static Button btnClearFiles;
        private static Button btnSendFiles;
        private static Border progressCard;
        private static ColumnDefinition colProgressFilled;
        private static ColumnDefinition colProgressEmpty;
        private static TextBlock progressText;
        private static TextBlock speedBadge;

        // Receive View controls
        private static StackPanel remoteFileStack;
        private static Button btnDownloadAll;
        private static TextBlock remoteEmptyText;

        // App state
        private static readonly Dictionary<string, DiscoveredDevice> discoveredDevices = new Dictionary<string, DiscoveredDevice>();
        private static readonly List<DiscoveredDevice> deviceList = new List<DiscoveredDevice>();
        private static DiscoveredDevice selectedDevice = null;
        private static readonly List<string> selectedFiles = new List<string>();
        private static readonly List<RemoteFileInfo> remoteFiles = new List<RemoteFileInfo>();
        private static UdpClient udpServer;
        private static volatile bool isRunning = true;
        private static volatile bool isScanning = false;
        private static string localIpAddress = "";
        private static string localSubnet = "";

        [STAThread]
        public static void Main()
        {
            try
            {
                Application app = new Application();
                // Crash Shield
                app.DispatcherUnhandledException += (s, e) =>
                {
                    e.Handled = true;
                };
                AppDomain.CurrentDomain.UnhandledException += (s, e) => { };

                DetectLocalNetwork();
                BuildUi();
                StartDiscovery();
                app.Run(mainWindow);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Startup error: " + ex.Message, "Zhare", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ================= TEMPLATE HELPERS =================
        public static ControlTemplate CreateRoundedButtonTemplate(int radius)
        {
            ControlTemplate template = new ControlTemplate(typeof(Button));
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.Name = "border";
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
            border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));

            FrameworkElementFactory cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(cp);

            template.VisualTree = border;
            return template;
        }

        public static ControlTemplate CreateRoundedTextBoxTemplate(int radius)
        {
            ControlTemplate template = new ControlTemplate(typeof(TextBox));
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.Name = "border";
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(TextBox.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(TextBox.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(TextBox.BorderThicknessProperty));

            FrameworkElementFactory sv = new FrameworkElementFactory(typeof(ScrollViewer));
            sv.Name = "PART_ContentHost";
            sv.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
            sv.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
            sv.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(sv);

            template.VisualTree = border;
            return template;
        }

        private static Button CreateSleekButton(string text, Brush normalBg, Brush hoverBg, Brush fg, int radius, Thickness padding, RoutedEventHandler onClick)
        {
            Button btn = new Button
            {
                Content = text,
                Background = normalBg,
                Foreground = fg,
                Padding = padding,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                FontWeight = FontWeights.SemiBold,
                FontSize = 13,
                SnapsToDevicePixels = true
            };
            btn.Template = CreateRoundedButtonTemplate(radius);
            btn.MouseEnter += (s, e) => { if (btn.IsEnabled) btn.Background = hoverBg; };
            btn.MouseLeave += (s, e) => { if (btn.IsEnabled) btn.Background = normalBg; };
            if (onClick != null) btn.Click += onClick;
            return btn;
        }

        private static Button CreateBorderedButton(string text, Brush normalBg, Brush hoverBg, Brush borderBrush, Brush hoverBorder, Brush fg, int radius, Thickness padding, RoutedEventHandler onClick)
        {
            Button btn = new Button
            {
                Content = text,
                Background = normalBg,
                BorderBrush = borderBrush,
                Foreground = fg,
                Padding = padding,
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                FontWeight = FontWeights.Medium,
                FontSize = 12.5,
                SnapsToDevicePixels = true
            };
            btn.Template = CreateRoundedButtonTemplate(radius);
            btn.MouseEnter += (s, e) =>
            {
                if (btn.IsEnabled)
                {
                    btn.Background = hoverBg;
                    if (hoverBorder != null) btn.BorderBrush = hoverBorder;
                }
            };
            btn.MouseLeave += (s, e) =>
            {
                if (btn.IsEnabled)
                {
                    btn.Background = normalBg;
                    if (borderBrush != null) btn.BorderBrush = borderBrush;
                }
            };
            if (onClick != null) btn.Click += onClick;
            return btn;
        }

        // ================= NETWORK DETECTION =================
        private static void DetectLocalNetwork()
        {
            try
            {
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus == OperationalStatus.Up &&
                        ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    {
                        string desc = (ni.Description + " " + ni.Name).ToLower();
                        if (desc.Contains("hamachi") || desc.Contains("virtual") || desc.Contains("vmware") ||
                            desc.Contains("vethernet") || desc.Contains("tap") || desc.Contains("wsl"))
                        {
                            continue;
                        }

                        foreach (UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
                        {
                            if (ip.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip.Address))
                            {
                                string ipStr = ip.Address.ToString();
                                if (ipStr.StartsWith("192.168.") || ipStr.StartsWith("10.") || ipStr.StartsWith("172."))
                                {
                                    localIpAddress = ipStr;
                                    localSubnet = ipStr.Substring(0, ipStr.LastIndexOf('.') + 1);
                                    return;
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            if (string.IsNullOrEmpty(localSubnet))
            {
                localSubnet = "192.168.18.";
                localIpAddress = "192.168.18.86";
            }
        }

        // ================= UI INITIALIZATION =================
        private static void BuildUi()
        {
            mainWindow = new Window
            {
                Title = "Zhare",
                Width = 880,
                Height = 740,
                MinWidth = 800,
                MinHeight = 640,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Segoe UI, -apple-system, sans-serif")
            };

            // Atmospheric Radial Gradient Background
            RadialGradientBrush rootBg = new RadialGradientBrush();
            rootBg.Center = new Point(0.5, 0.0);
            rootBg.GradientOrigin = new Point(0.5, 0.0);
            rootBg.RadiusX = 1.0;
            rootBg.RadiusY = 0.85;
            rootBg.GradientStops.Add(new GradientStop(Color.FromRgb(22, 31, 54), 0.0));
            rootBg.GradientStops.Add(new GradientStop(Color.FromRgb(14, 20, 36), 0.45));
            rootBg.GradientStops.Add(new GradientStop(Color.FromRgb(9, 13, 22), 1.0));
            mainWindow.Background = rootBg;

            // DWM Dark Title Bar
            mainWindow.SourceInitialized += (s, e) =>
            {
                try
                {
                    IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(mainWindow).Handle;
                    int trueVal = 1;
                    DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref trueVal, sizeof(int));
                    DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref trueVal, sizeof(int));
                    int darkColor = 0x00160F09; // BGR for #090F16
                    DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref darkColor, sizeof(int));
                }
                catch { }
            };

            Grid rootGrid = new Grid();
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(82) }); // Header
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(58) }); // Quick Connect & Utilities Bar
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(52) }); // Segmented Nav Tabs
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Main Content
            mainWindow.Content = rootGrid;

            // ================= 1. HEADER =================
            Border headerBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(160, 11, 16, 28)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(26, 36, 58)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(24, 14, 24, 14)
            };
            Grid.SetRow(headerBorder, 0);
            rootGrid.Children.Add(headerBorder);

            Grid headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Brand on left
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Device status on right
            headerBorder.Child = headerGrid;

            // Brand Section
            StackPanel brandStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            Border iconBadge = new Border
            {
                Width = 42,
                Height = 42,
                CornerRadius = new CornerRadius(12),
                Background = new LinearGradientBrush(Color.FromRgb(99, 102, 241), Color.FromRgb(6, 182, 212), 45),
                Margin = new Thickness(0, 0, 14, 0),
                Effect = new DropShadowEffect
                {
                    Color = Color.FromRgb(99, 102, 241),
                    BlurRadius = 16,
                    ShadowDepth = 0,
                    Opacity = 0.5
                }
            };
            TextBlock iconSymbol = new TextBlock
            {
                Text = "⚡",
                FontSize = 21,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            iconBadge.Child = iconSymbol;
            brandStack.Children.Add(iconBadge);

            StackPanel textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            StackPanel titleRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            TextBlock titleText = new TextBlock
            {
                Text = "Zhare",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 8, 0)
            };
            Border badgePill = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(120, 15, 41, 66)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(3, 105, 161)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(6, 2, 6, 2),
                VerticalAlignment = VerticalAlignment.Center
            };
            TextBlock badgeText = new TextBlock
            {
                Text = "P2P Beam",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248))
            };
            badgePill.Child = badgeText;
            titleRow.Children.Add(titleText);
            titleRow.Children.Add(badgePill);

            networkInfoText = new TextBlock
            {
                Text = string.Format("Wi-Fi Network • PC: {0}", localIpAddress),
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                Margin = new Thickness(0, 2, 0, 0)
            };
            textStack.Children.Add(titleRow);
            textStack.Children.Add(networkInfoText);
            brandStack.Children.Add(textStack);
            Grid.SetColumn(brandStack, 0);
            headerGrid.Children.Add(brandStack);

            // Right: Device Status Capsule
            deviceGlow = new DropShadowEffect
            {
                Color = Color.FromRgb(245, 158, 11),
                BlurRadius = 14,
                ShadowDepth = 0,
                Opacity = 0.35
            };

            deviceCapsule = new Border
            {
                CornerRadius = new CornerRadius(20),
                Background = new SolidColorBrush(Color.FromRgb(18, 25, 40)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(45, 58, 82)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14, 7, 16, 7),
                Cursor = Cursors.Hand,
                Effect = deviceGlow,
                VerticalAlignment = VerticalAlignment.Center
            };

            StackPanel capsuleContent = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            statusDot = new System.Windows.Shapes.Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = new SolidColorBrush(Color.FromRgb(245, 158, 11)), // Amber searching
                Margin = new Thickness(0, 0, 9, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            capsuleContent.Children.Add(statusDot);

            TextBlock phoneGlyph = new TextBlock
            {
                Text = "📱",
                FontSize = 13,
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            capsuleContent.Children.Add(phoneGlyph);

            statusDevName = new TextBlock
            {
                Text = "Scanning for phone...",
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                VerticalAlignment = VerticalAlignment.Center
            };
            capsuleContent.Children.Add(statusDevName);

            statusDevIp = new TextBlock
            {
                Text = "",
                FontSize = 11.5,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153)),
                Margin = new Thickness(6, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            capsuleContent.Children.Add(statusDevIp);

            statusDropdownArrow = new TextBlock
            {
                Text = " ▾",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            capsuleContent.Children.Add(statusDropdownArrow);

            deviceCapsule.Child = capsuleContent;

            // Clicking capsule switches device or rescans
            deviceCapsule.MouseLeftButtonUp += (s, e) =>
            {
                if (deviceList.Count > 1)
                {
                    ContextMenu menu = new ContextMenu
                    {
                        Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                        Foreground = Brushes.White
                    };
                    foreach (DiscoveredDevice d in deviceList)
                    {
                        MenuItem item = new MenuItem
                        {
                            Header = string.Format("📱 {0}  ({1})", d.DeviceName, d.IpAddress),
                            Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                            Foreground = Brushes.White,
                            Padding = new Thickness(10, 6, 10, 6)
                        };
                        DiscoveredDevice targetDev = d;
                        item.Click += (ms, me) => SetConnectedStatus(targetDev);
                        menu.Items.Add(item);
                    }
                    menu.PlacementTarget = deviceCapsule;
                    menu.IsOpen = true;
                }
                else
                {
                    SafeScanSubnet();
                }
            };

            Grid.SetColumn(deviceCapsule, 1);
            headerGrid.Children.Add(deviceCapsule);

            // ================= 2. QUICK CONNECT & UTILITY BAR (FIXED TEXT CLIPPING) =================
            Border utilBar = new Border
            {
                Padding = new Thickness(24, 8, 24, 8)
            };
            Grid.SetRow(utilBar, 1);
            rootGrid.Children.Add(utilBar);

            DockPanel utilDock = new DockPanel();
            utilBar.Child = utilDock;

            // Left: Spacious IP Input Capsule
            Border ipPill = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(200, 14, 21, 35)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(30, 42, 65)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(19),
                Height = 38,
                Padding = new Thickness(6, 2, 6, 2),
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(ipPill, Dock.Left);

            StackPanel ipStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            TextBlock ipIcon = new TextBlock
            {
                Text = "📡 Target IP:",
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                Margin = new Thickness(10, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            ipStack.Children.Add(ipIcon);

            txtDirectIp = new TextBox
            {
                Width = 155,
                Height = 30,
                Text = localSubnet,
                Background = new SolidColorBrush(Color.FromRgb(19, 27, 44)),
                Foreground = Brushes.White,
                CaretBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(10, 0, 10, 0),
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                VerticalAlignment = VerticalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            txtDirectIp.Template = CreateRoundedTextBoxTemplate(8);
            txtDirectIp.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    btnDirectConnect.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
            };
            ipStack.Children.Add(txtDirectIp);

            btnDirectConnect = CreateSleekButton(
                "Connect",
                new SolidColorBrush(Color.FromRgb(79, 70, 229)),
                new SolidColorBrush(Color.FromRgb(99, 102, 241)),
                Brushes.White,
                15,
                new Thickness(16, 0, 16, 0),
                (s, e) =>
                {
                    string target = txtDirectIp.Text.Trim();
                    if (!string.IsNullOrEmpty(target))
                    {
                        if (!target.Contains(".") && !string.IsNullOrEmpty(localSubnet))
                        {
                            target = localSubnet + target;
                            txtDirectIp.Text = target;
                        }
                        statusDevName.Text = "Connecting to " + target + "...";
                        statusDevIp.Text = "";
                        statusDot.Fill = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                        ThreadPool.QueueUserWorkItem(st => ProbeDevice(target));
                    }
                }
            );
            btnDirectConnect.Margin = new Thickness(8, 0, 0, 0);
            btnDirectConnect.Height = 30;
            btnDirectConnect.VerticalAlignment = VerticalAlignment.Center;
            ipStack.Children.Add(btnDirectConnect);

            ipPill.Child = ipStack;
            utilDock.Children.Add(ipPill);

            // Right: Utility Actions (Rescan + Open Downloads)
            StackPanel rightUtils = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };

            btnRescan = CreateBorderedButton(
                "⚡ Radar Scan",
                new SolidColorBrush(Color.FromArgb(160, 18, 26, 42)),
                new SolidColorBrush(Color.FromRgb(30, 42, 66)),
                new SolidColorBrush(Color.FromRgb(35, 48, 76)),
                new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                18,
                new Thickness(16, 0, 16, 0),
                (s, e) =>
                {
                    statusDevName.Text = "Scanning network...";
                    statusDevIp.Text = "";
                    statusDot.Fill = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                    SafeScanSubnet();
                }
            );
            btnRescan.Height = 36;
            btnRescan.VerticalAlignment = VerticalAlignment.Center;
            rightUtils.Children.Add(btnRescan);

            btnOpenDownloads = CreateBorderedButton(
                "📁 Received Files",
                new SolidColorBrush(Color.FromArgb(160, 18, 26, 42)),
                new SolidColorBrush(Color.FromRgb(30, 42, 66)),
                new SolidColorBrush(Color.FromRgb(35, 48, 76)),
                new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                18,
                new Thickness(16, 0, 16, 0),
                (s, e) =>
                {
                    string path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "Zhare");
                    if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                    Process.Start("explorer.exe", path);
                }
            );
            btnOpenDownloads.Height = 36;
            btnOpenDownloads.Margin = new Thickness(8, 0, 0, 0);
            btnOpenDownloads.VerticalAlignment = VerticalAlignment.Center;
            rightUtils.Children.Add(btnOpenDownloads);

            utilDock.Children.Add(rightUtils);

            // ================= 3. SEGMENTED NAVIGATION DOCK =================
            Border navContainer = new Border
            {
                Padding = new Thickness(24, 4, 24, 6),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            Grid.SetRow(navContainer, 2);
            rootGrid.Children.Add(navContainer);

            Border navCapsule = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(200, 12, 18, 30)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(26, 38, 60)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(3)
            };
            navContainer.Child = navCapsule;

            StackPanel navStack = new StackPanel { Orientation = Orientation.Horizontal };
            navCapsule.Child = navStack;

            // Nav Pill 1: Send
            navPillSend = new Border
            {
                CornerRadius = new CornerRadius(13),
                Padding = new Thickness(18, 7, 18, 7),
                Cursor = Cursors.Hand,
                Background = new LinearGradientBrush(Color.FromRgb(99, 102, 241), Color.FromRgb(79, 70, 229), 0),
                Effect = new DropShadowEffect
                {
                    Color = Color.FromRgb(99, 102, 241),
                    BlurRadius = 12,
                    ShadowDepth = 0,
                    Opacity = 0.4
                }
            };
            navTextSend = new TextBlock
            {
                Text = "📤  Beam to Phone",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White
            };
            navPillSend.Child = navTextSend;
            navPillSend.MouseLeftButtonUp += (s, e) => SwitchTab(true);
            navStack.Children.Add(navPillSend);

            // Nav Pill 2: Receive
            navPillReceive = new Border
            {
                CornerRadius = new CornerRadius(13),
                Padding = new Thickness(18, 7, 18, 7),
                Cursor = Cursors.Hand,
                Background = Brushes.Transparent,
                Margin = new Thickness(4, 0, 0, 0)
            };
            navTextReceive = new TextBlock
            {
                Text = "📥  Files on Phone",
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139))
            };
            navPillReceive.Child = navTextReceive;
            navPillReceive.MouseEnter += (s, e) =>
            {
                if (viewReceive != null && viewReceive.Visibility != Visibility.Visible)
                    navTextReceive.Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225));
            };
            navPillReceive.MouseLeave += (s, e) =>
            {
                if (viewReceive != null && viewReceive.Visibility != Visibility.Visible)
                    navTextReceive.Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139));
            };
            navPillReceive.MouseLeftButtonUp += (s, e) =>
            {
                SwitchTab(false);
                FetchPhoneFiles();
            };
            navStack.Children.Add(navPillReceive);

            // ================= 4. MAIN CONTENT AREA =================
            Grid contentGrid = new Grid { Margin = new Thickness(24, 6, 24, 20) };
            Grid.SetRow(contentGrid, 3);
            rootGrid.Children.Add(contentGrid);

            // VIEW 1: SEND
            viewSend = new Grid();
            BuildSendView(viewSend);
            contentGrid.Children.Add(viewSend);

            // VIEW 2: RECEIVE
            viewReceive = new Grid { Visibility = Visibility.Collapsed };
            BuildReceiveView(viewReceive);
            contentGrid.Children.Add(viewReceive);

            mainWindow.Closed += (s, e) =>
            {
                isRunning = false;
                try { if (udpServer != null) udpServer.Close(); } catch { }
            };
        }

        private static void SwitchTab(bool isSendActive)
        {
            if (isSendActive)
            {
                navPillSend.Background = new LinearGradientBrush(Color.FromRgb(99, 102, 241), Color.FromRgb(79, 70, 229), 0);
                navPillSend.Effect = new DropShadowEffect { Color = Color.FromRgb(99, 102, 241), BlurRadius = 12, ShadowDepth = 0, Opacity = 0.4 };
                navTextSend.Foreground = Brushes.White;
                navTextSend.FontWeight = FontWeights.SemiBold;

                navPillReceive.Background = Brushes.Transparent;
                navPillReceive.Effect = null;
                navTextReceive.Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139));
                navTextReceive.FontWeight = FontWeights.Medium;
            }
            else
            {
                navPillReceive.Background = new LinearGradientBrush(Color.FromRgb(99, 102, 241), Color.FromRgb(79, 70, 229), 0);
                navPillReceive.Effect = new DropShadowEffect { Color = Color.FromRgb(99, 102, 241), BlurRadius = 12, ShadowDepth = 0, Opacity = 0.4 };
                navTextReceive.Foreground = Brushes.White;
                navTextReceive.FontWeight = FontWeights.SemiBold;

                navPillSend.Background = Brushes.Transparent;
                navPillSend.Effect = null;
                navTextSend.Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139));
                navTextSend.FontWeight = FontWeights.Medium;
            }

            viewSend.Visibility = isSendActive ? Visibility.Visible : Visibility.Collapsed;
            viewReceive.Visibility = !isSendActive ? Visibility.Visible : Visibility.Collapsed;
        }

        // ================= SEND VIEW =================
        private static void BuildSendView(Grid parent)
        {
            parent.RowDefinitions.Add(new RowDefinition { Height = new GridLength(170) }); // Futuristic Drop Zone
            parent.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // File Queue
            parent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Bottom Actions / HUD

            // Futuristic Beam Drop Zone
            dropZoneGlow = new DropShadowEffect
            {
                Color = Color.FromRgb(6, 182, 212),
                BlurRadius = 24,
                ShadowDepth = 0,
                Opacity = 0.0
            };

            dropZone = new Border
            {
                CornerRadius = new CornerRadius(20),
                Background = new SolidColorBrush(Color.FromArgb(200, 14, 22, 38)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(30, 44, 72)),
                BorderThickness = new Thickness(1.5),
                Cursor = Cursors.Hand,
                AllowDrop = true,
                Effect = dropZoneGlow
            };
            Grid.SetRow(dropZone, 0);
            parent.Children.Add(dropZone);

            StackPanel dropStack = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Luminous central beam emblem
            Border iconCircle = new Border
            {
                Width = 52,
                Height = 52,
                CornerRadius = new CornerRadius(26),
                Background = new SolidColorBrush(Color.FromRgb(24, 35, 60)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(43, 59, 94)),
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8)
            };
            TextBlock iconSymbol = new TextBlock
            {
                Text = "⚡",
                FontSize = 24,
                Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            iconCircle.Child = iconSymbol;
            dropStack.Children.Add(iconCircle);

            TextBlock dropTitle = new TextBlock
            {
                Text = "Drop files anywhere to beam",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            dropStack.Children.Add(dropTitle);

            TextBlock dropSub = new TextBlock
            {
                Text = "or click anywhere to select photos, 4K videos, documents, or APKs",
                FontSize = 12.5,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 8)
            };
            dropStack.Children.Add(dropSub);

            Border speedPill = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(160, 10, 24, 42)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(3, 105, 161)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 3, 10, 3),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            TextBlock speedText = new TextBlock
            {
                Text = "⚡ High-Speed Direct P2P • No Internet Required • Zero Compression",
                FontSize = 11,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248))
            };
            speedPill.Child = speedText;
            dropStack.Children.Add(speedPill);

            dropZone.Child = dropStack;

            // Interactive Drag & Drop Animations
            dropZone.DragEnter += (s, e) =>
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    dropZone.BorderBrush = new SolidColorBrush(Color.FromRgb(6, 182, 212));
                    dropZone.Background = new SolidColorBrush(Color.FromArgb(240, 18, 30, 56));
                    dropZoneGlow.Opacity = 0.6;
                }
            };
            dropZone.DragLeave += (s, e) =>
            {
                dropZone.BorderBrush = new SolidColorBrush(Color.FromRgb(30, 44, 72));
                dropZone.Background = new SolidColorBrush(Color.FromArgb(200, 14, 22, 38));
                dropZoneGlow.Opacity = 0.0;
            };
            dropZone.Drop += (s, e) =>
            {
                dropZone.BorderBrush = new SolidColorBrush(Color.FromRgb(30, 44, 72));
                dropZone.Background = new SolidColorBrush(Color.FromArgb(200, 14, 22, 38));
                dropZoneGlow.Opacity = 0.0;
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    AddFiles(files);
                }
            };
            dropZone.MouseLeftButtonUp += (s, e) =>
            {
                OpenFileDialog ofd = new OpenFileDialog { Multiselect = true, Title = "Select Files to Beam to Phone" };
                if (ofd.ShowDialog() == true)
                {
                    AddFiles(ofd.FileNames);
                }
            };

            // Files Queue Card
            Border queueCard = new Border
            {
                CornerRadius = new CornerRadius(18),
                Background = new SolidColorBrush(Color.FromArgb(180, 12, 18, 32)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(26, 37, 60)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 12, 0, 12),
                Padding = new Thickness(16)
            };
            Grid.SetRow(queueCard, 1);
            parent.Children.Add(queueCard);

            Grid queueGrid = new Grid();
            queueGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
            queueGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            queueCard.Child = queueGrid;

            DockPanel queueHeader = new DockPanel();
            StackPanel qLeft = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            TextBlock queueTitle = new TextBlock
            {
                Text = "Queued for Beam",
                FontWeight = FontWeights.Bold,
                FontSize = 13.5,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                VerticalAlignment = VerticalAlignment.Center
            };
            qLeft.Children.Add(queueTitle);

            queueCountBadge = new TextBlock
            {
                Text = "",
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            qLeft.Children.Add(queueCountBadge);
            DockPanel.SetDock(qLeft, Dock.Left);
            queueHeader.Children.Add(qLeft);

            btnClearFiles = new Button
            {
                Content = "Clear All",
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113)),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            btnClearFiles.Template = CreateRoundedButtonTemplate(6);
            btnClearFiles.Click += (s, e) =>
            {
                selectedFiles.Clear();
                RefreshFileList();
            };
            queueHeader.Children.Add(btnClearFiles);
            Grid.SetRow(queueHeader, 0);
            queueGrid.Children.Add(queueHeader);

            ScrollViewer scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 8, 0, 0) };
            fileListStack = new StackPanel();
            scroll.Content = fileListStack;
            Grid.SetRow(scroll, 1);
            queueGrid.Children.Add(scroll);

            // Bottom Actions & Live Progress HUD
            Grid bottomGrid = new Grid { Margin = new Thickness(0, 2, 0, 0) };
            Grid.SetRow(bottomGrid, 2);
            parent.Children.Add(bottomGrid);

            btnSendFiles = new Button
            {
                Content = "⚡  Beam Files to Phone",
                Height = 48,
                Width = 320,
                HorizontalAlignment = HorizontalAlignment.Center,
                FontSize = 14.5,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Background = new LinearGradientBrush(Color.FromRgb(99, 102, 241), Color.FromRgb(6, 182, 212), 0),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Visibility = Visibility.Collapsed,
                Effect = new DropShadowEffect
                {
                    Color = Color.FromRgb(99, 102, 241),
                    BlurRadius = 18,
                    ShadowDepth = 0,
                    Opacity = 0.5
                }
            };
            btnSendFiles.Template = CreateRoundedButtonTemplate(24);
            btnSendFiles.Click += (s, e) => StartSendingFiles();
            bottomGrid.Children.Add(btnSendFiles);

            // Cyber Transfer HUD (Fully visible, rock-solid layout)
            progressCard = new Border
            {
                CornerRadius = new CornerRadius(16),
                Background = new SolidColorBrush(Color.FromArgb(230, 15, 23, 42)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(18, 14, 18, 14),
                Visibility = Visibility.Collapsed
            };

            Grid progGrid = new Grid();
            progGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            progGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            DockPanel progHeader = new DockPanel();
            progressText = new TextBlock
            {
                Text = "Beaming files...",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                MaxWidth = 600,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(progressText, Dock.Left);
            progHeader.Children.Add(progressText);

            speedBadge = new TextBlock
            {
                Text = "⚡ 0 MB/s",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            progHeader.Children.Add(speedBadge);
            Grid.SetRow(progHeader, 0);
            progGrid.Children.Add(progHeader);

            // Custom Hardware-Accelerated Progress Track (100% bug-proof)
            Border progTrack = new Border
            {
                Height = 8,
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Color.FromRgb(20, 28, 48)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(35, 48, 76)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 10, 0, 2),
                ClipToBounds = true
            };

            Grid progBarGrid = new Grid();
            colProgressFilled = new ColumnDefinition { Width = new GridLength(0, GridUnitType.Star) };
            colProgressEmpty = new ColumnDefinition { Width = new GridLength(100, GridUnitType.Star) };
            progBarGrid.ColumnDefinitions.Add(colProgressFilled);
            progBarGrid.ColumnDefinitions.Add(colProgressEmpty);

            Border progFill = new Border
            {
                CornerRadius = new CornerRadius(3),
                Background = new LinearGradientBrush(Color.FromRgb(99, 102, 241), Color.FromRgb(6, 182, 212), 0)
            };
            Grid.SetColumn(progFill, 0);
            progBarGrid.Children.Add(progFill);
            progTrack.Child = progBarGrid;

            Grid.SetRow(progTrack, 1);
            progGrid.Children.Add(progTrack);

            progressCard.Child = progGrid;
            bottomGrid.Children.Add(progressCard);

            RefreshFileList();
        }

        private static void AddFiles(string[] files)
        {
            foreach (string f in files)
            {
                if (File.Exists(f) && !selectedFiles.Contains(f))
                {
                    selectedFiles.Add(f);
                }
            }
            RefreshFileList();
        }

        private static void RefreshFileList()
        {
            fileListStack.Children.Clear();
            if (selectedFiles.Count == 0)
            {
                TextBlock empty = new TextBlock
                {
                    Text = "No files in queue. Drag files into the beam zone above.",
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                    FontSize = 13,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 44, 0, 0)
                };
                fileListStack.Children.Add(empty);
                btnSendFiles.Visibility = Visibility.Collapsed;
                queueCountBadge.Visibility = Visibility.Collapsed;
                btnClearFiles.Visibility = Visibility.Collapsed;
                return;
            }

            btnClearFiles.Visibility = Visibility.Visible;
            btnSendFiles.Visibility = Visibility.Visible;

            long totalBytes = 0;
            foreach (string file in selectedFiles)
            {
                long len = 0;
                try { len = new FileInfo(file).Length; } catch { }
                totalBytes += len;

                Border card = new Border
                {
                    CornerRadius = new CornerRadius(12),
                    Background = new SolidColorBrush(Color.FromRgb(19, 27, 44)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(30, 44, 70)),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(12, 9, 12, 9),
                    Margin = new Thickness(0, 0, 0, 6)
                };

                DockPanel dock = new DockPanel();

                // Remove Button
                Button btnRemove = new Button
                {
                    Content = "✕",
                    Background = Brushes.Transparent,
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand,
                    Width = 26,
                    Height = 26,
                    FontSize = 12,
                    FontWeight = FontWeights.Bold
                };
                btnRemove.Template = CreateRoundedButtonTemplate(13);
                string curFile = file;
                btnRemove.MouseEnter += (s, e) =>
                {
                    btnRemove.Background = new SolidColorBrush(Color.FromArgb(60, 239, 68, 68));
                    btnRemove.Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113));
                };
                btnRemove.MouseLeave += (s, e) =>
                {
                    btnRemove.Background = Brushes.Transparent;
                    btnRemove.Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139));
                };
                btnRemove.Click += (s, e) =>
                {
                    selectedFiles.Remove(curFile);
                    RefreshFileList();
                };
                DockPanel.SetDock(btnRemove, Dock.Right);
                dock.Children.Add(btnRemove);

                // File Details
                StackPanel info = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                Border iconBadge = new Border
                {
                    Width = 32,
                    Height = 32,
                    CornerRadius = new CornerRadius(8),
                    Background = new SolidColorBrush(Color.FromRgb(28, 38, 60)),
                    Margin = new Thickness(0, 0, 10, 0)
                };
                TextBlock icon = new TextBlock
                {
                    Text = GetFileIcon(curFile),
                    FontSize = 15,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                iconBadge.Child = icon;
                info.Children.Add(iconBadge);

                TextBlock name = new TextBlock
                {
                    Text = System.IO.Path.GetFileName(curFile),
                    FontSize = 13.5,
                    FontWeight = FontWeights.Medium,
                    Foreground = Brushes.White,
                    MaxWidth = 520,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center
                };
                info.Children.Add(name);

                TextBlock size = new TextBlock
                {
                    Text = " • " + FormatBytes(len),
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                    VerticalAlignment = VerticalAlignment.Center
                };
                info.Children.Add(size);
                dock.Children.Add(info);

                card.Child = dock;
                fileListStack.Children.Add(card);
            }

            queueCountBadge.Text = string.Format("•  {0} file{1} ({2})",
                selectedFiles.Count,
                selectedFiles.Count == 1 ? "" : "s",
                FormatBytes(totalBytes));
            queueCountBadge.Visibility = Visibility.Visible;

            UpdateSendButtonText();
        }

        private static void UpdateSendButtonText()
        {
            if (btnSendFiles == null) return;
            string targetName = (selectedDevice != null) ? selectedDevice.DeviceName : "Phone";
            btnSendFiles.Content = string.Format("⚡  Beam {0} File{1} to {2}",
                selectedFiles.Count,
                selectedFiles.Count == 1 ? "" : "s",
                targetName);
        }

        // ================= RECEIVE VIEW =================
        private static void BuildReceiveView(Grid parent)
        {
            parent.RowDefinitions.Add(new RowDefinition { Height = new GridLength(44) }); // Controls
            parent.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // File List
            parent.RowDefinitions.Add(new RowDefinition { Height = new GridLength(56) }); // Actions

            DockPanel top = new DockPanel();
            TextBlock title = new TextBlock
            {
                Text = "Files Available on Connected Phone",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(title, Dock.Left);
            top.Children.Add(title);

            Button btnRefresh = CreateBorderedButton(
                "🔄  Refresh",
                new SolidColorBrush(Color.FromRgb(22, 31, 50)),
                new SolidColorBrush(Color.FromRgb(32, 45, 72)),
                new SolidColorBrush(Color.FromRgb(35, 48, 76)),
                new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                Brushes.White,
                10,
                new Thickness(14, 6, 14, 6),
                (s, e) => FetchPhoneFiles()
            );
            btnRefresh.HorizontalAlignment = HorizontalAlignment.Right;
            top.Children.Add(btnRefresh);
            Grid.SetRow(top, 0);
            parent.Children.Add(top);

            // Remote List Card
            Border card = new Border
            {
                CornerRadius = new CornerRadius(18),
                Background = new SolidColorBrush(Color.FromArgb(180, 12, 18, 32)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(26, 37, 60)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 6, 0, 14)
            };
            Grid.SetRow(card, 1);
            parent.Children.Add(card);

            ScrollViewer scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            remoteFileStack = new StackPanel();
            scroll.Content = remoteFileStack;
            card.Child = scroll;

            remoteEmptyText = new TextBlock
            {
                Text = "No files shared by phone yet.\nSelect files in Zhare on your phone to see them here.",
                TextAlignment = TextAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                FontSize = 13.5,
                Margin = new Thickness(0, 90, 0, 0)
            };
            remoteFileStack.Children.Add(remoteEmptyText);

            // Bottom Actions
            DockPanel bottom = new DockPanel();
            btnDownloadAll = CreateSleekButton(
                "⬇️  Download All to PC",
                new LinearGradientBrush(Color.FromRgb(99, 102, 241), Color.FromRgb(79, 70, 229), 0),
                new LinearGradientBrush(Color.FromRgb(129, 140, 248), Color.FromRgb(99, 102, 241), 0),
                Brushes.White,
                14,
                new Thickness(24, 10, 24, 10),
                (s, e) => DownloadAllPhoneFiles()
            );
            btnDownloadAll.Height = 44;
            DockPanel.SetDock(btnDownloadAll, Dock.Left);
            bottom.Children.Add(btnDownloadAll);

            Button btnOpenFolder = CreateBorderedButton(
                "📁  Open Downloads Folder",
                new SolidColorBrush(Color.FromRgb(22, 31, 50)),
                new SolidColorBrush(Color.FromRgb(32, 45, 72)),
                new SolidColorBrush(Color.FromRgb(35, 48, 76)),
                new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                Brushes.White,
                14,
                new Thickness(18, 10, 18, 10),
                (s, e) =>
                {
                    string path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "Zhare");
                    if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                    Process.Start("explorer.exe", path);
                }
            );
            btnOpenFolder.Height = 44;
            btnOpenFolder.HorizontalAlignment = HorizontalAlignment.Right;
            bottom.Children.Add(btnOpenFolder);

            Grid.SetRow(bottom, 2);
            parent.Children.Add(bottom);
        }

        // ================= SAFE DISCOVERY ENGINE =================
        private static void StartDiscovery()
        {
            // 1. Listen for UDP broadcast
            Thread listenThread = new Thread(new ThreadStart(ListenUdp)) { IsBackground = true };
            listenThread.Start();

            // 2. Gateway probe & paced Subnet sweep loop
            Thread probeThread = new Thread(new ThreadStart(AutoScanLoop)) { IsBackground = true };
            probeThread.Start();
        }

        private static void ListenUdp()
        {
            try
            {
                udpServer = new UdpClient();
                udpServer.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udpServer.Client.Bind(new IPEndPoint(IPAddress.Any, 8889));

                while (isRunning)
                {
                    IPEndPoint ep = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = udpServer.Receive(ref ep);
                    string msg = Encoding.UTF8.GetString(data);

                    if (msg.Contains("HOTSPOT_SHARE_BEACON") || msg.Contains("ZHARE_BEACON"))
                    {
                        string name = ExtractJson(msg, "deviceName");
                        if (string.IsNullOrEmpty(name)) name = "Android Smartphone";
                        RegisterFoundDevice(new DiscoveredDevice
                        {
                            DeviceName = name,
                            DeviceType = "android",
                            IpAddress = ep.Address.ToString(),
                            Port = 8888,
                            LastSeen = DateTime.Now
                        });
                    }
                }
            }
            catch { }
        }

        private static void AutoScanLoop()
        {
            Thread.Sleep(800);
            SafeScanSubnet();

            while (isRunning)
            {
                Thread.Sleep(15000);
                SafeScanSubnet();
            }
        }

        // 100% crash-proof subnet scan using dedicated background thread & non-blocking socket poll
        private static void SafeScanSubnet()
        {
            if (isScanning) return;
            isScanning = true;

            Thread scanThread = new Thread(() =>
            {
                try
                {
                    // 1. Hotspot IP
                    CheckAndProbe("192.168.43.1");

                    // 2. Default Gateways
                    try
                    {
                        foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                        {
                            if (ni.OperationalStatus == OperationalStatus.Up)
                            {
                                foreach (GatewayIPAddressInformation g in ni.GetIPProperties().GatewayAddresses)
                                {
                                    string gIp = g.Address.ToString();
                                    if (!gIp.StartsWith("127.") && gIp != "192.168.43.1")
                                    {
                                        CheckAndProbe(gIp);
                                    }
                                }
                            }
                        }
                    }
                    catch { }

                    // 3. Scan local subnet with paced non-blocking socket checks
                    if (!string.IsNullOrEmpty(localSubnet))
                    {
                        for (int i = 1; i <= 254; i++)
                        {
                            if (!isRunning) break;
                            string target = localSubnet + i;
                            CheckAndProbe(target);
                            Thread.Sleep(2);
                        }
                    }
                }
                catch { }
                finally
                {
                    isScanning = false;
                }
            })
            {
                IsBackground = true
            };
            scanThread.Start();
        }

        private static void CheckAndProbe(string target)
        {
            try
            {
                using (Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    sock.Blocking = false;
                    try
                    {
                        sock.Connect(new IPEndPoint(IPAddress.Parse(target), 8888));
                    }
                    catch (SocketException se)
                    {
                        if (se.ErrorCode != 10035) return; // 10035 is WSAEWOULDBLOCK
                    }

                    if (sock.Poll(60000, SelectMode.SelectWrite)) // 60ms timeout
                    {
                        ProbeDevice(target);
                    }
                }
            }
            catch { }
        }

        private static void ProbeDevice(string ip)
        {
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(string.Format("http://{0}:8888/api/info", ip));
                req.Timeout = 1500;
                req.ReadWriteTimeout = 1500;
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                {
                    if (resp.StatusCode == HttpStatusCode.OK)
                    {
                        using (StreamReader reader = new StreamReader(resp.GetResponseStream()))
                        {
                            string json = reader.ReadToEnd();
                            string devName = ExtractJson(json, "device");
                            if (string.IsNullOrEmpty(devName)) devName = "Smartphone";

                            RegisterFoundDevice(new DiscoveredDevice
                            {
                                DeviceName = devName,
                                DeviceType = "android",
                                IpAddress = ip,
                                Port = 8888,
                                LastSeen = DateTime.Now
                            });
                        }
                    }
                }
            }
            catch { }
        }

        private static void RegisterFoundDevice(DiscoveredDevice dev)
        {
            if (mainWindow == null || !isRunning) return;

            try
            {
                mainWindow.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        string key = dev.IpAddress + ":" + dev.Port;
                        bool isNew = !discoveredDevices.ContainsKey(key);
                        discoveredDevices[key] = dev;

                        if (isNew)
                        {
                            deviceList.Add(dev);
                            if (selectedDevice == null)
                            {
                                selectedDevice = dev;
                            }
                            SetConnectedStatus(selectedDevice);
                            txtDirectIp.Text = selectedDevice.IpAddress;
                            UpdateSendButtonText();
                        }
                    }
                    catch { }
                }));
            }
            catch { }
        }

        private static void SetConnectedStatus(DiscoveredDevice dev)
        {
            try
            {
                if (dev == null)
                {
                    statusDot.Fill = new SolidColorBrush(Color.FromRgb(245, 158, 11)); // Amber
                    deviceGlow.Color = Color.FromRgb(245, 158, 11);
                    deviceGlow.Opacity = 0.35;
                    deviceCapsule.Background = new SolidColorBrush(Color.FromRgb(18, 25, 40));
                    deviceCapsule.BorderBrush = new SolidColorBrush(Color.FromRgb(45, 58, 82));
                    statusDevName.Text = "Scanning network...";
                    statusDevIp.Text = "";
                    statusDropdownArrow.Visibility = Visibility.Collapsed;
                }
                else
                {
                    selectedDevice = dev;
                    statusDot.Fill = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // Neon Emerald
                    deviceGlow.Color = Color.FromRgb(16, 185, 129);
                    deviceGlow.Opacity = 0.55;
                    deviceCapsule.Background = new SolidColorBrush(Color.FromArgb(220, 12, 36, 27));
                    deviceCapsule.BorderBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                    statusDevName.Text = dev.DeviceName;
                    statusDevIp.Text = dev.IpAddress;
                    statusDropdownArrow.Visibility = (deviceList.Count > 1) ? Visibility.Visible : Visibility.Collapsed;
                    txtDirectIp.Text = dev.IpAddress;
                    UpdateSendButtonText();
                }
            }
            catch { }
        }

        private static DiscoveredDevice GetSelectedDevice()
        {
            if (selectedDevice != null) return selectedDevice;

            // Fallback: If user entered an IP in the box
            string manualIp = (txtDirectIp != null) ? txtDirectIp.Text.Trim() : "";
            if (!string.IsNullOrEmpty(manualIp) && manualIp.Contains("."))
            {
                return new DiscoveredDevice { DeviceName = "Smartphone", IpAddress = manualIp, Port = 8888 };
            }
            return null;
        }

        // ================= TRANSFER ACTIONS (STREAMING 0-RAM DIRECT TCP) =================
        private static void StartSendingFiles()
        {
            DiscoveredDevice dev = GetSelectedDevice();
            if (dev == null)
            {
                MessageBox.Show("No phone detected.\nEnter your phone's IP address in the box above (shown on the phone app) and click Connect.", "Enter Phone IP", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (selectedFiles.Count == 0) return;

            btnSendFiles.Visibility = Visibility.Collapsed;
            progressCard.Visibility = Visibility.Visible;
            colProgressFilled.Width = new GridLength(0, GridUnitType.Star);
            colProgressEmpty.Width = new GridLength(100, GridUnitType.Star);
            progressText.Text = "Connecting to phone...";
            speedBadge.Text = "⚡ 0 MB/s";
            speedBadge.Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248));

            ThreadPool.QueueUserWorkItem((state) =>
            {
                List<string> toSend = new List<string>(selectedFiles);
                bool allSucceeded = true;
                string lastError = "";

                for (int i = 0; i < toSend.Count; i++)
                {
                    string file = toSend[i];
                    string url = string.Format("http://{0}:{1}/api/upload", dev.IpAddress, dev.Port);
                    string err = UploadFileStream(url, file, i + 1, toSend.Count);
                    if (!string.IsNullOrEmpty(err))
                    {
                        allSucceeded = false;
                        lastError = err;
                        break;
                    }
                }

                mainWindow.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (allSucceeded)
                    {
                        progressCard.Visibility = Visibility.Collapsed;
                        selectedFiles.Clear();
                        RefreshFileList();
                        MessageBox.Show("Files transferred successfully to your phone's Downloads folder!", "Transfer Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        btnSendFiles.Visibility = Visibility.Visible;
                        speedBadge.Text = "Failed";
                        speedBadge.Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113));
                        progressText.Text = "Transfer failed: " + lastError;
                        MessageBox.Show("Transfer failed:\n" + lastError + "\n\nMake sure the Zhare app is open on your phone.", "Transfer Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }));
            });
        }

        private static string UploadFileStream(string uploadUrl, string filePath, int currentIdx, int total)
        {
            FileStream fs = null;
            Stream reqStream = null;
            HttpWebResponse resp = null;

            try
            {
                FileInfo fi = new FileInfo(filePath);
                if (!fi.Exists) return "File does not exist: " + filePath;
                long fileSize = fi.Length;

                string boundary = "----ZhareBoundary" + DateTime.Now.Ticks.ToString("x");
                string header = string.Format(
                    "--{0}\r\nContent-Disposition: form-data; name=\"file\"; filename=\"{1}\"\r\nContent-Type: application/octet-stream\r\n\r\n",
                    boundary,
                    System.IO.Path.GetFileName(filePath));
                byte[] headerBytes = Encoding.UTF8.GetBytes(header);
                byte[] trailerBytes = Encoding.ASCII.GetBytes("\r\n--" + boundary + "--\r\n");

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uploadUrl);
                request.Method = "POST";
                request.ContentType = "multipart/form-data; boundary=" + boundary;
                request.KeepAlive = true;
                request.AllowWriteStreamBuffering = false; // Zero RAM buffering: streams directly to TCP socket!
                request.Timeout = 3600000; // 1-hour timeout for massive files (10GB+)
                request.ReadWriteTimeout = 600000; // 10 minutes per chunk
                request.ServicePoint.Expect100Continue = false; // Prevents NanoHTTPD handshake timeout
                request.ContentLength = headerBytes.Length + fileSize + trailerBytes.Length;

                reqStream = request.GetRequestStream();
                reqStream.Write(headerBytes, 0, headerBytes.Length);

                fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                byte[] buffer = new byte[256 * 1024]; // 256 KB chunk for high throughput
                int read;
                long totalRead = 0;
                DateTime lastTime = DateTime.Now;
                long prevBytes = 0;

                while ((read = fs.Read(buffer, 0, buffer.Length)) != 0)
                {
                    reqStream.Write(buffer, 0, read);
                    totalRead += read;

                    double sec = (DateTime.Now - lastTime).TotalSeconds;
                    if (sec >= 0.3)
                    {
                        double mbps = ((totalRead - prevBytes) / sec) / (1024 * 1024);
                        int pct = (fileSize > 0) ? (int)((totalRead * 100) / fileSize) : 100;
                        int safePct = Math.Max(0, Math.Min(100, pct));
                        string fname = System.IO.Path.GetFileName(filePath);

                        mainWindow.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            progressText.Text = string.Format("Beaming {0} ({1}/{2}) • {3}%", fname, currentIdx, total, safePct);
                            speedBadge.Text = string.Format("⚡ {0:0.#} MB/s", mbps);
                            colProgressFilled.Width = new GridLength(safePct, GridUnitType.Star);
                            colProgressEmpty.Width = new GridLength(100 - safePct, GridUnitType.Star);
                        }));

                        lastTime = DateTime.Now;
                        prevBytes = totalRead;
                    }
                }

                reqStream.Write(trailerBytes, 0, trailerBytes.Length);
                reqStream.Flush();

                resp = (HttpWebResponse)request.GetResponse();
                if (resp.StatusCode != HttpStatusCode.OK)
                {
                    return "Server returned: " + resp.StatusDescription;
                }

                mainWindow.Dispatcher.BeginInvoke(new Action(() =>
                {
                    colProgressFilled.Width = new GridLength(100, GridUnitType.Star);
                    colProgressEmpty.Width = new GridLength(0, GridUnitType.Star);
                }));

                return null; // Succeeded!
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
            finally
            {
                if (fs != null) { try { fs.Close(); } catch { } }
                if (reqStream != null) { try { reqStream.Close(); } catch { } }
                if (resp != null) { try { resp.Close(); } catch { } }
            }
        }

        private static void FetchPhoneFiles()
        {
            DiscoveredDevice dev = GetSelectedDevice();
            if (dev == null) return;

            ThreadPool.QueueUserWorkItem((s) =>
            {
                try
                {
                    string url = string.Format("http://{0}:{1}/api/files", dev.IpAddress, dev.Port);
                    HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                    req.Timeout = 3000;
                    using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                    using (StreamReader reader = new StreamReader(resp.GetResponseStream()))
                    {
                        string json = reader.ReadToEnd();
                        List<RemoteFileInfo> list = ParseRemoteFiles(json);

                        mainWindow.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            remoteFiles.Clear();
                            remoteFiles.AddRange(list);
                            RenderRemoteFiles();
                        }));
                    }
                }
                catch { }
            });
        }

        private static void RenderRemoteFiles()
        {
            remoteFileStack.Children.Clear();
            if (remoteFiles.Count == 0)
            {
                remoteFileStack.Children.Add(remoteEmptyText);
                btnDownloadAll.IsEnabled = false;
                btnDownloadAll.Opacity = 0.5;
                return;
            }

            btnDownloadAll.IsEnabled = true;
            btnDownloadAll.Opacity = 1.0;

            foreach (RemoteFileInfo rf in remoteFiles)
            {
                Border card = new Border
                {
                    CornerRadius = new CornerRadius(12),
                    Background = new SolidColorBrush(Color.FromRgb(19, 27, 44)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(30, 44, 70)),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(14, 10, 14, 10),
                    Margin = new Thickness(0, 0, 0, 6)
                };

                DockPanel dock = new DockPanel();

                Button btnDl = CreateSleekButton(
                    "⬇️ Save",
                    new SolidColorBrush(Color.FromRgb(79, 70, 229)),
                    new SolidColorBrush(Color.FromRgb(99, 102, 241)),
                    Brushes.White,
                    8,
                    new Thickness(14, 5, 14, 5),
                    null
                );
                RemoteFileInfo currentRf = rf;
                btnDl.Click += (s, e) => DownloadSingleFile(currentRf);
                DockPanel.SetDock(btnDl, Dock.Right);
                dock.Children.Add(btnDl);

                StackPanel sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                Border iconBadge = new Border
                {
                    Width = 32,
                    Height = 32,
                    CornerRadius = new CornerRadius(8),
                    Background = new SolidColorBrush(Color.FromRgb(28, 38, 60)),
                    Margin = new Thickness(0, 0, 10, 0)
                };
                TextBlock icon = new TextBlock
                {
                    Text = GetFileIcon(rf.Name),
                    FontSize = 15,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                iconBadge.Child = icon;
                sp.Children.Add(iconBadge);

                TextBlock name = new TextBlock
                {
                    Text = rf.Name,
                    FontSize = 13.5,
                    FontWeight = FontWeights.Medium,
                    Foreground = Brushes.White,
                    MaxWidth = 480,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center
                };
                TextBlock size = new TextBlock
                {
                    Text = " • " + rf.FormattedSize,
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                    VerticalAlignment = VerticalAlignment.Center
                };
                sp.Children.Add(name);
                sp.Children.Add(size);
                dock.Children.Add(sp);

                card.Child = dock;
                remoteFileStack.Children.Add(card);
            }
        }

        private static void DownloadSingleFile(RemoteFileInfo rf)
        {
            DiscoveredDevice dev = GetSelectedDevice();
            if (dev == null) return;

            string targetDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "Zhare");
            if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

            ThreadPool.QueueUserWorkItem((s) =>
            {
                try
                {
                    string dlUrl = string.Format("http://{0}:{1}/api/download/{2}", dev.IpAddress, dev.Port, rf.Id);
                    string dest = System.IO.Path.Combine(targetDir, rf.Name);

                    HttpWebRequest req = (HttpWebRequest)WebRequest.Create(dlUrl);
                    req.Timeout = 3600000; // 1-hour timeout for large downloads
                    req.ReadWriteTimeout = 600000;
                    using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                    using (Stream inStream = resp.GetResponseStream())
                    using (FileStream outStream = new FileStream(dest, FileMode.Create, FileAccess.Write))
                    {
                        byte[] buffer = new byte[128 * 1024];
                        int read;
                        while ((read = inStream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            outStream.Write(buffer, 0, read);
                        }
                    }

                    mainWindow.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        MessageBox.Show(string.Format("Downloaded {0} into Downloads\\Zhare!", rf.Name), "Download Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                    }));
                }
                catch (Exception ex)
                {
                    mainWindow.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        MessageBox.Show("Download failed: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }));
                }
            });
        }

        private static void DownloadAllPhoneFiles()
        {
            foreach (RemoteFileInfo rf in remoteFiles)
            {
                DownloadSingleFile(rf);
            }
        }

        // ================= HELPERS =================
        private static string GetFileIcon(string filename)
        {
            string ext = System.IO.Path.GetExtension(filename).ToLower().TrimStart('.');
            if (ext == "jpg" || ext == "jpeg" || ext == "png" || ext == "webp" || ext == "gif" || ext == "bmp") return "🖼️";
            if (ext == "mp4" || ext == "mkv" || ext == "mov" || ext == "avi" || ext == "webm") return "🎬";
            if (ext == "mp3" || ext == "wav" || ext == "m4a" || ext == "flac" || ext == "ogg") return "🎵";
            if (ext == "pdf") return "📕";
            if (ext == "doc" || ext == "docx" || ext == "txt" || ext == "xlsx" || ext == "pptx") return "📄";
            if (ext == "zip" || ext == "rar" || ext == "7z" || ext == "tar" || ext == "gz") return "📦";
            if (ext == "apk") return "🤖";
            return "📁";
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes <= 0) return "0 B";
            string[] units = new string[] { "B", "KB", "MB", "GB" };
            int digitGroups = (int)(Math.Log10(bytes) / Math.Log10(1024));
            if (digitGroups >= units.Length) digitGroups = units.Length - 1;
            double val = bytes / Math.Pow(1024, digitGroups);
            return string.Format("{0:0.##} {1}", val, units[digitGroups]);
        }

        private static string ExtractJson(string json, string key)
        {
            Match m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*\"?([^,\"}]+)\"?");
            return m.Success ? m.Groups[1].Value : null;
        }

        private static List<RemoteFileInfo> ParseRemoteFiles(string json)
        {
            List<RemoteFileInfo> list = new List<RemoteFileInfo>();
            MatchCollection matches = Regex.Matches(json, "\\{[^}]*\\}");
            foreach (Match m in matches)
            {
                string block = m.Value;
                string idStr = ExtractJson(block, "id");
                string name = ExtractJson(block, "name");
                string sizeStr = ExtractJson(block, "size");
                int id;
                long size;
                if (int.TryParse(idStr, out id) && !string.IsNullOrEmpty(name))
                {
                    long.TryParse(sizeStr, out size);
                    list.Add(new RemoteFileInfo { Id = id, Name = name, Size = size });
                }
            }
            return list;
        }
    }
}
