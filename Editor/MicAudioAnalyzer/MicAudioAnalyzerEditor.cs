using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MicAudioAnalyzer))]
public class MicAudioAnalyzerEditor : Editor
{
    private MicAudioAnalyzer _analyzer;
    private bool _isPlaying;           // 是否处于播放模式
    private const float BarHeight = 20f; // 特征条高度（参考示例的20px）
    private const float BarSpacing = 5f; // 特征条间距


    private void OnEnable()
    {
        _analyzer = (MicAudioAnalyzer)target;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
    }

    private void OnPlayModeChanged(PlayModeStateChange state)
    {
        _isPlaying = state == PlayModeStateChange.EnteredPlayMode;
        if (_isPlaying)
        {
            EditorApplication.update += RefreshInspector; // 播放时实时刷新
        }
        else
        {
            EditorApplication.update -= RefreshInspector;
        }
    }

    private void RefreshInspector() => Repaint();


    // ==================== 核心：参考示例的分层布局 ====================
    public override void OnInspectorGUI()
    {
        // 1. 第一层：麦克风设备管理（移到最上方）
        DrawMicrophoneSection();
        EditorGUILayout.Space(10); // 区块间距
 
 
        // 2. 第二层：默认配置项（原脚本的所有参数）
        DrawDefaultInspector();
        EditorGUILayout.Space(10); // 区块间距
 
 
        // 3. 第三层：实时音频特征可视化
        DrawRealTimeFeatures();
    }


    #region 参考示例布局：麦克风管理区块
    private void DrawMicrophoneSection()
    {
        // 区块标题
        EditorGUILayout.LabelField("🎤 麦克风管理", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;

        // 刷新设备按钮（参考示例的顶部按钮）
        if (GUILayout.Button("🔄 刷新麦克风列表"))
        {
            _analyzer.RefreshMicrophoneDevices();
            EditorUtility.SetDirty(_analyzer);
        }

        // 设备下拉菜单（参考示例的选中逻辑）
        if (_analyzer.microphoneDevices != null && _analyzer.microphoneDevices.Length > 0)
        {
            int newIndex = EditorGUILayout.Popup(
                "选中设备", 
                _analyzer.selectedMicIndex, 
                _analyzer.microphoneDevices
            );
            if (newIndex != _analyzer.selectedMicIndex)
            {
                Undo.RecordObject(_analyzer, "切换麦克风");
                _analyzer.selectedMicIndex = newIndex;
                EditorUtility.SetDirty(_analyzer);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("⚠️ 未检测到麦克风设备", MessageType.Warning);
        }

        EditorGUI.indentLevel--;
    }
    #endregion


    #region 参考示例布局：实时特征可视化（RMS/Peak垂直+频段水平）
    private void DrawRealTimeFeatures()
    {
        EditorGUILayout.LabelField("📊 音频特征", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;

        // 1. 检查是否有有效数据（使用default值检查而不是null检查）
        bool hasValidData = _analyzer._isRecording && (_analyzer.currentFeatures.normalizedRms != default || _analyzer.currentFeatures.normalizedPeak != default);

        if (!hasValidData)
        {
            // 显示默认状态（0值）
            DrawFeatureBar("RMS", 0f);
            DrawFeatureBar("Peak", 0f);
            EditorGUILayout.Space(5);
        
            EditorGUILayout.LabelField("频段能量", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            DrawFeatureBar("Low", 0f);
            DrawFeatureBar("Mid", 0f);
            DrawFeatureBar("High", 0f);
            EditorGUILayout.EndHorizontal();
        
            EditorGUILayout.HelpBox("未检测到音频数据", MessageType.Info);
        }
        else
        {
            // 2. RMS + Peak（参考示例的垂直排列）
            DrawFeatureBar("RMS", _analyzer.currentFeatures.normalizedRms);
            DrawFeatureBar("Peak", _analyzer.currentFeatures.normalizedPeak);
            EditorGUILayout.Space(5);

            // 3. 低中高频（参考示例的水平排列）
            EditorGUILayout.LabelField("频段能量", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            DrawFeatureBar("Low", _analyzer.currentFeatures.lowBandEnergy);
            DrawFeatureBar("Mid", _analyzer.currentFeatures.midBandEnergy);
            DrawFeatureBar("High", _analyzer.currentFeatures.highBandEnergy);
            EditorGUILayout.EndHorizontal();
        }

        EditorGUI.indentLevel--;
    }
    #endregion




    #region 参考示例布局：单个特征条（标签+背景+填充+数值）
    // Editor脚本中的DrawFeatureBar方法（完全替换）
    private void DrawFeatureBar(string label, float value)
    {
        EditorGUILayout.BeginHorizontal();

        // 1. 特征标签（加宽到50px，避免短文本裁切）
        EditorGUILayout.LabelField(label, GUILayout.Width(50));

        // 2. 特征条背景（自适应宽度，占满中间所有空间）
        Rect bgRect = GUILayoutUtility.GetRect(0, BarHeight, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(bgRect, new Color(0.2f, 0.2f, 0.2f));

        // 3. 特征条填充（半透绿色，根据值拉伸）
        float clampedValue = Mathf.Clamp01(value);
        Rect fillRect = new(bgRect.x, bgRect.y, bgRect.width * clampedValue, bgRect.height);
        EditorGUI.DrawRect(fillRect, new Color(0, 1, 0, 0.8f));

        // 4. 数值显示（加宽到50px，避免两位小数裁切）
        EditorGUILayout.LabelField(clampedValue.ToString("F2"), GUILayout.Width(50));

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(BarSpacing);
    }

    #endregion
}
