using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;
using MCPForUnity.Editor.Data;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Models;
using MCPForUnity.Editor.Services;

namespace MCPForUnity.Editor.Windows
{
    public class MCPForUnityEditorWindow : EditorWindow
    {
        // Protocol enum for future HTTP support
        private enum ConnectionProtocol
        {
            Stdio,
            // HTTPStreaming // Future
        }

        // Validation levels matching the existing enum
        private enum ValidationLevel
        {
            Basic,
            Standard,
            Comprehensive,
            Strict
        }

        // Data
        private readonly McpClients mcpClients = new McpClients();
        private int selectedClientIndex = 0;
        private ValidationLevel currentValidationLevel = ValidationLevel.Standard;
        private ConnectionProtocol connectionProtocol = ConnectionProtocol.Stdio;

        // UI State
        private bool debugLogs = false;
        private bool advancedSettingsFoldout = false;
        private bool manualConfigFoldout = false;
        private Vector2 scrollPosition;

        // Cached values
        private string mcpServerPathOverride = "";
        private string uvPathOverride = "";
        private string claudeCliPath = "";
        private string configPath = "";
        private string configJson = "";
        private string installationSteps = "";

        // Connection state
        private bool isConnected = false;
        private string healthStatus = "Unknown";
        private Color healthColor = Color.gray;

        // Server status
        private bool showServerBanner = false;
        private string serverBannerMessage = "";

        // Styles (initialized in OnEnable)
        private GUIStyle headerStyle;
        private GUIStyle sectionHeaderStyle;
        private GUIStyle boxStyle;
        private GUIStyle statusDotStyle;
        private GUIStyle buttonStyle;
        private GUIStyle warningBoxStyle;
        private bool stylesInitialized = false;

        [MenuItem("Window/MCP For Unity/Open MCP Window")]
        public static void ShowWindow()
        {
            var window = GetWindow<MCPForUnityEditorWindow>("MCP For Unity");
            window.minSize = new Vector2(500, 600);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
            InitializeData();
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnFocus()
        {
            RefreshAllData();
        }

        private void OnEditorUpdate()
        {
            UpdateConnectionStatus();
        }

        private void InitializeStyles()
        {
            if (stylesInitialized) return;

            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleLeft
            };

            sectionHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                padding = new RectOffset(0, 0, 10, 5)
            };

            boxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(10, 10, 10, 10),
                margin = new RectOffset(0, 0, 5, 5)
            };

            warningBoxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(10, 10, 10, 10),
                margin = new RectOffset(0, 0, 5, 5),
                normal = { background = MakeTex(2, 2, new Color(1f, 0.7f, 0f, 0.2f)) }
            };

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                padding = new RectOffset(10, 10, 8, 8)
            };

            statusDotStyle = new GUIStyle()
            {
                fixedWidth = 12,
                fixedHeight = 12,
                margin = new RectOffset(0, 5, 2, 0)
            };

            stylesInitialized = true;
        }

        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++)
                pix[i] = col;

            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        private void InitializeData()
        {
            // Load settings
            debugLogs = EditorPrefs.GetBool("MCPForUnity.DebugLogs", false);
            int savedLevel = EditorPrefs.GetInt("MCPForUnity.ValidationLevel", 1);
            currentValidationLevel = (ValidationLevel)Mathf.Clamp(savedLevel, 0, 3);

            // Initialize client dropdown
            if (mcpClients.clients.Count > 0)
            {
                selectedClientIndex = 0;
            }

            RefreshAllData();
        }

        private void RefreshAllData()
        {
            UpdateConnectionStatus();
            UpdatePathOverrides();
            UpdateServerStatusBanner();

            if (selectedClientIndex >= 0 && selectedClientIndex < mcpClients.clients.Count)
            {
                var client = mcpClients.clients[selectedClientIndex];
                MCPServiceLocator.Client.CheckClientStatus(client);
                UpdateManualConfiguration();
            }

            // Auto-verify bridge health if connected
            if (MCPServiceLocator.Bridge.IsRunning)
            {
                VerifyBridgeConnection();
            }
        }

        private void OnGUI()
        {
            InitializeStyles();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            DrawHeader();
            DrawServerStatusBanner();
            DrawSettingsSection();
            DrawConnectionSection();
            DrawClientConfigSection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(boxStyle);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("MCP For Unity", headerStyle);
            GUILayout.FlexibleSpace();
            
            string currentVersion = AssetPathUtility.GetPackageVersion();
            var updateCheck = MCPServiceLocator.Updates.CheckForUpdate(currentVersion);
            
            if (updateCheck.UpdateAvailable && !string.IsNullOrEmpty(updateCheck.LatestVersion))
            {
                GUI.color = new Color(1f, 0.7f, 0f);
                GUILayout.Label($"↑ v{currentVersion} (Update: v{updateCheck.LatestVersion})", EditorStyles.miniLabel);
                GUI.color = Color.white;
            }
            else
            {
                GUILayout.Label($"v{currentVersion}", EditorStyles.miniLabel);
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }

        private void DrawServerStatusBanner()
        {
            if (!showServerBanner) return;

            EditorGUILayout.BeginVertical(warningBoxStyle);
            EditorGUILayout.LabelField(serverBannerMessage, EditorStyles.wordWrappedLabel);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            bool hasEmbedded = ServerInstaller.HasEmbeddedServer();
            if (hasEmbedded)
            {
                if (GUILayout.Button("Rebuild Server", GUILayout.Width(150)))
                {
                    OnRebuildServerClicked();
                }
            }
            else
            {
                if (GUILayout.Button("Download & Install Server", GUILayout.Width(200)))
                {
                    OnDownloadServerClicked();
                }
            }
            
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            
            GUILayout.Space(5);
        }

        private void DrawSettingsSection()
        {
            EditorGUILayout.BeginVertical(boxStyle);
            
            GUILayout.Label("Settings", sectionHeaderStyle);
            
            // Debug Logs Toggle
            bool newDebugLogs = EditorGUILayout.Toggle("Show Debug Logs", debugLogs);
            if (newDebugLogs != debugLogs)
            {
                debugLogs = newDebugLogs;
                EditorPrefs.SetBool("MCPForUnity.DebugLogs", debugLogs);
            }
            
            // Validation Level
            ValidationLevel newValidationLevel = (ValidationLevel)EditorGUILayout.EnumPopup(
                "Script Validation Level", 
                currentValidationLevel
            );
            if (newValidationLevel != currentValidationLevel)
            {
                currentValidationLevel = newValidationLevel;
                EditorPrefs.SetInt("MCPForUnity.ValidationLevel", (int)currentValidationLevel);
            }
            
            // Validation description
            EditorGUILayout.HelpBox(GetValidationLevelDescription((int)currentValidationLevel), MessageType.Info);
            
            // Advanced Settings Foldout
            GUILayout.Space(5);
            advancedSettingsFoldout = EditorGUILayout.Foldout(advancedSettingsFoldout, "Advanced Settings", true);
            
            if (advancedSettingsFoldout)
            {
                EditorGUI.indentLevel++;
                DrawAdvancedSettings();
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.EndVertical();
        }

        private void DrawAdvancedSettings()
        {
            // MCP Server Path Override
            EditorGUILayout.LabelField("MCP Server Path Override", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.TextField(mcpServerPathOverride);
            
            if (GUILayout.Button("Browse", GUILayout.Width(70)))
            {
                OnBrowsePythonClicked();
            }
            if (GUILayout.Button("Clear", GUILayout.Width(60)))
            {
                OnClearPythonClicked();
            }
            EditorGUILayout.EndHorizontal();
            
            // UV Path Override
            EditorGUILayout.LabelField("UV Path Override", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.TextField(uvPathOverride);
            
            if (GUILayout.Button("Browse", GUILayout.Width(70)))
            {
                OnBrowseUvClicked();
            }
            if (GUILayout.Button("Clear", GUILayout.Width(60)))
            {
                OnClearUvClicked();
            }
            EditorGUILayout.EndHorizontal();
            
            GUILayout.Space(5);
        }

        private void DrawConnectionSection()
        {
            EditorGUILayout.BeginVertical(boxStyle);
            
            GUILayout.Label("Connection", sectionHeaderStyle);
            
            // Protocol (disabled for now)
            GUI.enabled = false;
            connectionProtocol = (ConnectionProtocol)EditorGUILayout.EnumPopup("Protocol", connectionProtocol);
            GUI.enabled = true;
            
            // Ports (read-only)
            EditorGUILayout.LabelField("Unity Port", MCPServiceLocator.Bridge.CurrentPort.ToString());
            EditorGUILayout.LabelField("Server Port", "6500");
            
            GUILayout.Space(5);
            
            // Connection Status
            EditorGUILayout.BeginHorizontal();
            DrawStatusDot(isConnected ? Color.green : Color.red);
            EditorGUILayout.LabelField(isConnected ? "Connected" : "Disconnected", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();
            
            // Connection Toggle Button
            Color originalColor = GUI.backgroundColor;
            GUI.backgroundColor = isConnected ? new Color(1f, 0.5f, 0.5f) : new Color(0.5f, 1f, 0.5f);
            
            if (GUILayout.Button(isConnected ? "Stop" : "Start", buttonStyle, GUILayout.Height(35)))
            {
                OnConnectionToggleClicked();
            }
            
            GUI.backgroundColor = originalColor;
            
            GUILayout.Space(10);
            
            // Health Check
            EditorGUILayout.LabelField("Health Check", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            DrawStatusDot(healthColor);
            EditorGUILayout.LabelField(healthStatus);
            EditorGUILayout.EndHorizontal();
            
            if (GUILayout.Button("Test Connection", GUILayout.Height(30)))
            {
                OnTestConnectionClicked();
            }
            
            EditorGUILayout.EndVertical();
        }

        private void DrawClientConfigSection()
        {
            EditorGUILayout.BeginVertical(boxStyle);
            
            GUILayout.Label("Client Configuration", sectionHeaderStyle);
            
            // Client Dropdown
            if (mcpClients.clients.Count > 0)
            {
                var clientNames = mcpClients.clients.Select(c => c.name).ToArray();
                int newIndex = EditorGUILayout.Popup("Select Client", selectedClientIndex, clientNames);
                
                if (newIndex != selectedClientIndex)
                {
                    selectedClientIndex = newIndex;
                    UpdateManualConfiguration();
                }
                
                GUILayout.Space(5);
                
                // Configure All Button
                if (GUILayout.Button("Configure All Detected Clients", GUILayout.Height(30)))
                {
                    OnConfigureAllClientsClicked();
                }
                
                GUILayout.Space(10);
                
                // Selected Client Status
                if (selectedClientIndex >= 0 && selectedClientIndex < mcpClients.clients.Count)
                {
                    var client = mcpClients.clients[selectedClientIndex];
                    
                    EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
                    EditorGUILayout.BeginHorizontal();
                    
                    Color statusColor = GetClientStatusColor(client.status);
                    DrawStatusDot(statusColor);
                    EditorGUILayout.LabelField(client.GetStatusDisplayString());
                    
                    EditorGUILayout.EndHorizontal();
                    
                    // Configure Button
                    string buttonText = client.mcpType == McpTypes.ClaudeCode && client.status == McpStatus.Configured 
                        ? "Unregister" 
                        : "Configure";
                    
                    if (GUILayout.Button(buttonText, GUILayout.Height(30)))
                    {
                        OnConfigureClicked();
                    }
                    
                    // Claude CLI Path (only for Claude Code)
                    if (client.mcpType == McpTypes.ClaudeCode)
                    {
                        GUILayout.Space(5);
                        EditorGUILayout.LabelField("Claude CLI Path", EditorStyles.boldLabel);
                        
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.TextField(claudeCliPath);
                        
                        if (GUILayout.Button("Browse", GUILayout.Width(70)))
                        {
                            OnBrowseClaudeClicked();
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                    
                    GUILayout.Space(10);
                    
                    // Manual Configuration Foldout
                    manualConfigFoldout = EditorGUILayout.Foldout(manualConfigFoldout, "Manual Configuration", true);
                    
                    if (manualConfigFoldout)
                    {
                        EditorGUI.indentLevel++;
                        DrawManualConfiguration();
                        EditorGUI.indentLevel--;
                    }
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No MCP clients detected.", MessageType.Info);
            }
            
            EditorGUILayout.EndVertical();
        }

        private void DrawManualConfiguration()
        {
            // Config Path
            EditorGUILayout.LabelField("Configuration File Path", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.TextField(configPath);
            
            if (GUILayout.Button("Copy", GUILayout.Width(60)))
            {
                OnCopyPathClicked();
            }
            if (GUILayout.Button("Open", GUILayout.Width(60)))
            {
                OnOpenFileClicked();
            }
            EditorGUILayout.EndHorizontal();
            
            GUILayout.Space(5);
            
            // Config JSON
            EditorGUILayout.LabelField("Configuration JSON", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.TextArea(configJson, GUILayout.Height(100));
            
            EditorGUILayout.BeginVertical(GUILayout.Width(60));
            if (GUILayout.Button("Copy"))
            {
                OnCopyJsonClicked();
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndHorizontal();
            
            GUILayout.Space(5);
            
            // Installation Steps
            EditorGUILayout.LabelField("Installation Steps", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(installationSteps, MessageType.Info);
        }

        private void DrawStatusDot(Color color)
        {
            Rect rect = GUILayoutUtility.GetRect(12, 12, GUILayout.Width(12));
            EditorGUI.DrawRect(rect, color);
        }

        private Color GetClientStatusColor(McpStatus status)
        {
            switch (status)
            {
                case McpStatus.Configured:
                case McpStatus.Running:
                case McpStatus.Connected:
                    return Color.green;
                case McpStatus.IncorrectPath:
                case McpStatus.CommunicationError:
                case McpStatus.NoResponse:
                    return new Color(1f, 0.7f, 0f); // Orange
                default:
                    return Color.red;
            }
        }

        private string GetValidationLevelDescription(int index)
        {
            return index switch
            {
                0 => "Only basic syntax checks (braces, quotes, comments)",
                1 => "Syntax checks + Unity best practices and warnings",
                2 => "All checks + semantic analysis and performance warnings",
                3 => "Full semantic validation with namespace/type resolution (requires Roslyn)",
                _ => "Standard validation"
            };
        }

        // Update Methods
        private void UpdateConnectionStatus()
        {
            var bridgeService = MCPServiceLocator.Bridge;
            isConnected = bridgeService.IsRunning;
            
            if (!isConnected)
            {
                healthStatus = "Unknown";
                healthColor = Color.gray;
            }
        }

        private void UpdatePathOverrides()
        {
            var pathService = MCPServiceLocator.Paths;

            // MCP Server Path
            string mcpServerPath = pathService.GetMcpServerPath();
            if (pathService.HasMcpServerOverride)
            {
                mcpServerPathOverride = mcpServerPath ?? "(override set but invalid)";
            }
            else
            {
                mcpServerPathOverride = mcpServerPath ?? "(auto-detected)";
            }

            // UV Path
            string uvPath = pathService.GetUvPath();
            if (pathService.HasUvPathOverride)
            {
                uvPathOverride = uvPath ?? "(override set but invalid)";
            }
            else
            {
                uvPathOverride = uvPath ?? "(auto-detected)";
            }
        }

        private void UpdateServerStatusBanner()
        {
            bool hasEmbedded = ServerInstaller.HasEmbeddedServer();
            string installedVer = ServerInstaller.GetInstalledServerVersion();
            string packageVer = AssetPathUtility.GetPackageVersion();

            // Check for installation errors first
            string installError = PackageLifecycleManager.GetLastInstallError();
            if (!string.IsNullOrEmpty(installError))
            {
                serverBannerMessage = $"✖ Server installation failed: {installError}. Click 'Rebuild Server' to retry.";
                showServerBanner = true;
            }
            // Update banner
            else if (!hasEmbedded && string.IsNullOrEmpty(installedVer))
            {
                serverBannerMessage = "⚠ Server not installed. Click 'Download & Install Server' to get started.";
                showServerBanner = true;
            }
            else if (!hasEmbedded && !string.IsNullOrEmpty(installedVer) && installedVer != packageVer)
            {
                serverBannerMessage = $"⚠ Server update available (v{installedVer} → v{packageVer}). Update recommended.";
                showServerBanner = true;
            }
            else
            {
                showServerBanner = false;
            }
        }

        private void UpdateManualConfiguration()
        {
            if (selectedClientIndex < 0 || selectedClientIndex >= mcpClients.clients.Count)
                return;

            var client = mcpClients.clients[selectedClientIndex];

            // Get config path
            configPath = MCPServiceLocator.Client.GetConfigPath(client);

            // Get config JSON
            configJson = MCPServiceLocator.Client.GenerateConfigJson(client);

            // Get installation steps
            installationSteps = MCPServiceLocator.Client.GetInstallationSteps(client);

            // Update Claude CLI path if applicable
            if (client.mcpType == McpTypes.ClaudeCode)
            {
                string path = MCPServiceLocator.Paths.GetClaudeCliPath();
                claudeCliPath = string.IsNullOrEmpty(path) ? "Not found - click Browse to select" : path;
            }
        }

        // Button Callbacks
        private void OnConnectionToggleClicked()
        {
            var bridgeService = MCPServiceLocator.Bridge;

            if (bridgeService.IsRunning)
            {
                bridgeService.Stop();
            }
            else
            {
                bridgeService.Start();

                // Verify connection after starting
                EditorApplication.delayCall += () =>
                {
                    if (bridgeService.IsRunning)
                    {
                        VerifyBridgeConnection();
                    }
                };
            }

            UpdateConnectionStatus();
            Repaint();
        }

        private void OnTestConnectionClicked()
        {
            VerifyBridgeConnection();
            Repaint();
        }

        private void VerifyBridgeConnection()
        {
            var bridgeService = MCPServiceLocator.Bridge;

            if (!bridgeService.IsRunning)
            {
                healthStatus = "Disconnected";
                healthColor = Color.gray;
                McpLog.Warn("Cannot verify connection: Bridge is not running");
                return;
            }

            var result = bridgeService.Verify(bridgeService.CurrentPort);

            if (result.Success && result.PingSucceeded)
            {
                healthStatus = "Healthy";
                healthColor = Color.green;
                McpLog.Info("Bridge verification successful");
            }
            else if (result.HandshakeValid)
            {
                healthStatus = "Ping Failed";
                healthColor = new Color(1f, 0.7f, 0f); // Orange
                McpLog.Warn($"Bridge verification warning: {result.Message}");
            }
            else
            {
                healthStatus = "Unhealthy";
                healthColor = Color.red;
                McpLog.Error($"Bridge verification failed: {result.Message}");
            }
        }

        private void OnDownloadServerClicked()
        {
            if (ServerInstaller.DownloadAndInstallServer())
            {
                UpdateServerStatusBanner();
                UpdatePathOverrides();
                EditorUtility.DisplayDialog(
                    "Download Complete",
                    "Server installed successfully! Start your connection and configure your MCP clients to begin.",
                    "OK"
                );
                Repaint();
            }
        }

        private void OnRebuildServerClicked()
        {
            try
            {
                bool success = ServerInstaller.RebuildMcpServer();
                if (success)
                {
                    EditorUtility.DisplayDialog("MCP For Unity", "Server rebuilt successfully.", "OK");
                    UpdateServerStatusBanner();
                    UpdatePathOverrides();
                    Repaint();
                }
                else
                {
                    EditorUtility.DisplayDialog("MCP For Unity", "Rebuild failed. Please check Console for details.", "OK");
                }
            }
            catch (Exception ex)
            {
                McpLog.Error($"Failed to rebuild server: {ex.Message}");
                EditorUtility.DisplayDialog("MCP For Unity", $"Rebuild failed: {ex.Message}", "OK");
            }
        }

        private void OnConfigureAllClientsClicked()
        {
            try
            {
                var summary = MCPServiceLocator.Client.ConfigureAllDetectedClients();

                // Build detailed message
                string message = summary.GetSummaryMessage() + "\n\n";
                foreach (var msg in summary.Messages)
                {
                    message += msg + "\n";
                }

                EditorUtility.DisplayDialog("Configure All Clients", message, "OK");

                // Refresh current client status
                if (selectedClientIndex >= 0 && selectedClientIndex < mcpClients.clients.Count)
                {
                    RefreshAllData();
                    Repaint();
                }
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Configuration Failed", ex.Message, "OK");
            }
        }

        private void OnConfigureClicked()
        {
            if (selectedClientIndex < 0 || selectedClientIndex >= mcpClients.clients.Count)
                return;

            var client = mcpClients.clients[selectedClientIndex];

            try
            {
                if (client.mcpType == McpTypes.ClaudeCode)
                {
                    bool isConfigured = client.status == McpStatus.Configured;
                    if (isConfigured)
                    {
                        MCPServiceLocator.Client.UnregisterClaudeCode();
                    }
                    else
                    {
                        MCPServiceLocator.Client.RegisterClaudeCode();
                    }
                }
                else
                {
                    MCPServiceLocator.Client.ConfigureClient(client);
                }

                RefreshAllData();
                Repaint();
            }
            catch (Exception ex)
            {
                McpLog.Error($"Configuration failed: {ex.Message}");
                EditorUtility.DisplayDialog("Configuration Failed", ex.Message, "OK");
            }
        }

        private void OnBrowsePythonClicked()
        {
            string picked = EditorUtility.OpenFolderPanel("Select MCP Server Directory", Application.dataPath, "");
            if (!string.IsNullOrEmpty(picked))
            {
                try
                {
                    MCPServiceLocator.Paths.SetMcpServerOverride(picked);
                    UpdatePathOverrides();
                    McpLog.Info($"MCP server path override set to: {picked}");
                    Repaint();
                }
                catch (Exception ex)
                {
                    EditorUtility.DisplayDialog("Invalid Path", ex.Message, "OK");
                }
            }
        }

        private void OnClearPythonClicked()
        {
            MCPServiceLocator.Paths.ClearMcpServerOverride();
            UpdatePathOverrides();
            McpLog.Info("MCP server path override cleared");
            Repaint();
        }

        private void OnBrowseUvClicked()
        {
            string suggested = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? "/opt/homebrew/bin"
                : Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string picked = EditorUtility.OpenFilePanel("Select UV Executable", suggested, "");
            if (!string.IsNullOrEmpty(picked))
            {
                try
                {
                    MCPServiceLocator.Paths.SetUvPathOverride(picked);
                    UpdatePathOverrides();
                    McpLog.Info($"UV path override set to: {picked}");
                    Repaint();
                }
                catch (Exception ex)
                {
                    EditorUtility.DisplayDialog("Invalid Path", ex.Message, "OK");
                }
            }
        }

        private void OnClearUvClicked()
        {
            MCPServiceLocator.Paths.ClearUvPathOverride();
            UpdatePathOverrides();
            McpLog.Info("UV path override cleared");
            Repaint();
        }

        private void OnBrowseClaudeClicked()
        {
            string suggested = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? "/opt/homebrew/bin"
                : Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string picked = EditorUtility.OpenFilePanel("Select Claude CLI", suggested, "");
            if (!string.IsNullOrEmpty(picked))
            {
                try
                {
                    MCPServiceLocator.Paths.SetClaudeCliPathOverride(picked);
                    UpdateManualConfiguration();
                    RefreshAllData();
                    McpLog.Info($"Claude CLI path override set to: {picked}");
                    Repaint();
                }
                catch (Exception ex)
                {
                    EditorUtility.DisplayDialog("Invalid Path", ex.Message, "OK");
                }
            }
        }

        private void OnCopyPathClicked()
        {
            EditorGUIUtility.systemCopyBuffer = configPath;
            McpLog.Info("Config path copied to clipboard");
        }

        private void OnOpenFileClicked()
        {
            string path = configPath;
            try
            {
                if (!File.Exists(path))
                {
                    EditorUtility.DisplayDialog("Open File", "The configuration file path does not exist.", "OK");
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                McpLog.Error($"Failed to open file: {ex.Message}");
            }
        }

        private void OnCopyJsonClicked()
        {
            EditorGUIUtility.systemCopyBuffer = configJson;
            McpLog.Info("Configuration copied to clipboard");
        }
    }
}
