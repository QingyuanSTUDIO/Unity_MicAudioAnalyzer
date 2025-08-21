using UnityEngine;
using System;

public class MicAudioAnalyzer : MonoBehaviour
{
    // ==================== 音频特征结构（汉化） ====================
    [Serializable]
    public struct AudioMotionFeatures
    {
        [Tooltip("平均响度（RMS）：0=静音，1=最大响度")] public float normalizedRms;
        [Tooltip("峰值响度：0=无冲击，1=最大峰值")] public float normalizedPeak;
        [Tooltip("低频能量（0-300Hz）：0=无，1=最大")] public float lowBandEnergy;
        [Tooltip("中频能量（300Hz-4kHz）：0=无，1=最大")] public float midBandEnergy;
        [Tooltip("高频能量（>4kHz）：0=无，1=最大")] public float highBandEnergy;
    }


    // ==================== 麦克风配置（汉化） ====================
    [Header("🎤 麦克风设置")]
    [HideInInspector] public string[] microphoneDevices;
    [HideInInspector] public int selectedMicIndex = 0;
    [Tooltip("麦克风采样率（建议44100）")] public int sampleRate = 44100;
    [Tooltip("麦克风缓存时长（秒）")] public int maxRecordLength = 10;


    // ==================== 分析配置（汉化） ====================
    [Header("🔬 分析参数")]
    [Tooltip("FFT大小（自动调整为2的幂）")] public int fftSize = 256;
    [Tooltip("RMS计算窗口（秒，越小响应越快）")] public float rmsWindow = 0.1f;
    [Tooltip("静音阈值：低于此值视为无输入")] public float silenceThreshold = 0.001f;


    // ==================== 平滑配置（新增） ====================
    [Header("📊 平滑配置")]
    [Tooltip("平滑系数（0~1，值越小越平滑，响应越慢）")]
    [Range(0.1f, 0.9f)] public float filterSmoothing = 0.3f; // 推荐0.2~0.5


    // ==================== 内部状态 ====================
    private AudioClip _micClip;               // 麦克风录制的音频
    public bool _isRecording = false;        // 是否正在录制
    private float[] _timeData;                // RMS窗口的时域数据（响度计算）
    private float[] _fftTimeData;             // FFT用的时域数据（频谱计算）
    private Complex[] _complexBuffer;         // FFT复数缓存
    private float[] _spectrumData;            // 频谱数据（幅度）
    private float _freqPerBin;                // 每个FFT Bin对应的频率（Hz）
    public AudioMotionFeatures currentFeatures; // 当前音频特征
    private string _currentMicDevice;// 保存当前正在录制的设备名称

    // 平滑用的历史值（新增）
    private float _lastNormalizedRms;
    private float _lastNormalizedPeak;
    private float _lastLowBandEnergy;
    private float _lastMidBandEnergy;
    private float _lastHighBandEnergy;


    // ==================== 生命周期 ====================
    void Start()
    {
        RefreshMicrophoneDevices();
        StartRecording();
        InitializeBuffers();
    }

    void Update()
    {
        if (!_isRecording) return;
        UpdateAudioData();   // 读取实时音频数据
        ComputeFeatures();   // 计算特征+平滑
    }

    void OnDestroy() => StopRecording();


    // ==================== 麦克风管理 ====================
    [ContextMenu("刷新麦克风列表")]
    public void RefreshMicrophoneDevices()
    {
        string previousDevice = _currentMicDevice; // 保存之前的设备
        microphoneDevices = Microphone.devices;
        if (microphoneDevices.Length == 0)
        {
            Debug.LogWarning("⚠️ 未检测到麦克风！");
            StopRecording(); // 无设备时停止录制
            return;
        }
        // 调整选中索引至有效范围
        int oldIndex = selectedMicIndex;
        selectedMicIndex = Mathf.Clamp(selectedMicIndex, 0, microphoneDevices.Length - 1);
        // 若原设备已移除且正在录制，自动切换至新设备
        if (_isRecording && oldIndex != selectedMicIndex)
        {
            Debug.Log($"⚠️ 原麦克风已移除，自动切换至：{microphoneDevices[selectedMicIndex]}");
            SwitchMicrophone(selectedMicIndex); // 自动切换
        }
    }

    public void StartRecording()
    {
        if (_isRecording || microphoneDevices.Length == 0) return;
    
        // 修复：保存当前启动的设备名称（后续停止时需用此名称）
        _currentMicDevice = microphoneDevices[selectedMicIndex]; 
        _micClip = Microphone.Start(_currentMicDevice, true, maxRecordLength, sampleRate);
    
        _isRecording = true;
        Debug.Log($"✅ 开始录制：{_currentMicDevice}");
    }

    public void StopRecording()
    {
        if (!_isRecording) return;
        // 修复：用启动时的设备名称停止录制（关键！）
        if (!string.IsNullOrEmpty(_currentMicDevice) && Microphone.IsRecording(_currentMicDevice))
        {
            Microphone.End(_currentMicDevice); // 正确释放原设备资源
        }
        Destroy(_micClip);
        _isRecording = false;
        _currentMicDevice = null; // 重置当前设备
    }
    
    
    /// <summary>
    /// 切换麦克风设备（外部调用此方法实现切换）
    /// </summary>
    /// <param name="newIndex">新设备的索引（从0开始）</param>
    public void SwitchMicrophone(int newIndex)
    {
        // 1. 校验设备列表有效性
        if (microphoneDevices == null || microphoneDevices.Length == 0)
        {
            Debug.LogWarning("⚠️ 无可用麦克风设备！");
            return;
        }
        // 2. 校验索引有效性
        newIndex = Mathf.Clamp(newIndex, 0, microphoneDevices.Length - 1);
        if (newIndex == selectedMicIndex)
        {
            Debug.Log("🔹 已选中该麦克风，无需切换！");
            return;
        }
        // 3. 核心流程：停止旧设备 → 切换索引 → 启动新设备
        StopRecording();              // 必须先停旧的！
        selectedMicIndex = newIndex;  // 更新选中索引
        StartRecording();             // 启动新设备
        Debug.Log($"🔄 成功切换至麦克风：{microphoneDevices[newIndex]}");
    }


    // ==================== 缓存初始化 ====================
    private void InitializeBuffers()
    {
        // 1. RMS窗口数据：RMS窗口内的样本数（如0.1秒×44100=4410）
        int rmsSampleCount = Mathf.RoundToInt(rmsWindow * sampleRate);
        _timeData = new float[rmsSampleCount];

        // 2. FFT数据：确保FFT大小是2的幂
        fftSize = Mathf.NextPowerOfTwo(fftSize);
        _fftTimeData = new float[fftSize];       // FFT用的时域数据
        _complexBuffer = new Complex[fftSize];   // FFT复数缓存
        _spectrumData = new float[fftSize / 2];  // 频谱数据（对称，取前半部分）

        // 3. 每个FFT Bin的频率（Hz）：采样率 / FFT大小
        _freqPerBin = sampleRate / (float)fftSize;

        // 初始化平滑历史值（新增）
        _lastNormalizedRms = 0f;
        _lastNormalizedPeak = 0f;
        _lastLowBandEnergy = 0f;
        _lastMidBandEnergy = 0f;
        _lastHighBandEnergy = 0f;
    }


    // ==================== 实时数据读取 ====================
    private void UpdateAudioData()
    {
        if (_micClip == null) return;

        // 1. 获取麦克风当前录制位置
        int micPos = Microphone.GetPosition(microphoneDevices[selectedMicIndex]);
        if (micPos < 0) return;

        // 2. 读取RMS窗口的时域数据（用于响度计算）
        int rmsStart = (micPos - _timeData.Length + _micClip.samples) % _micClip.samples;
        _micClip.GetData(_timeData, rmsStart);

        // 3. 读取FFT用的时域数据（用于频谱计算）
        int fftStart = (micPos - fftSize + _micClip.samples) % _micClip.samples;
        _micClip.GetData(_fftTimeData, fftStart);

        // 4. 时域转频域（自实现FFT）
        RunFFT();
    }


    // ==================== 自实现FFT（核心） ====================
    private void RunFFT()
    {
        // 1. 将时域实数转换为复数（虚部为0）
        for (int i = 0; i < fftSize; i++)
            _complexBuffer[i] = new Complex(_fftTimeData[i], 0f);

        // 2. 执行FFT变换
        FFT.Compute(_complexBuffer, invert: false);

        // 3. 计算频谱幅度（与Unity GetSpectrumData结果一致）
        for (int i = 0; i < _spectrumData.Length; i++)
        {
            float real = _complexBuffer[i].Real;
            float imag = _complexBuffer[i].Imag;
            _spectrumData[i] = (real * real + imag * imag) / (fftSize / 2f);
        }
    }


    // ==================== 特征计算+平滑（核心优化） ====================
    private void ComputeFeatures()
    {
        if (_timeData == null || _timeData.Length == 0) return;

        // 1. 计算原始响度（RMS+峰值）
        float rms = CalculateRMS(_timeData);
        float peak = CalculatePeak(_timeData);
        float rawRms = NormalizeLoudness(rms);
        float rawPeak = NormalizeLoudness(peak);

        // 2. 应用平滑滤波（响度）
        currentFeatures.normalizedRms = ApplyLowPassFilter(rawRms, ref _lastNormalizedRms);
        currentFeatures.normalizedPeak = ApplyLowPassFilter(rawPeak, ref _lastNormalizedPeak);

        // 3. 计算频谱能量（高中低）+ 平滑
        CalculateSpectrumEnergy();
    }

    /// <summary> 计算高中低三频能量（含平滑） </summary>
    private void CalculateSpectrumEnergy()
    {
        if (_spectrumData == null || _spectrumData.Length == 0) return;

        // 1. 计算各频段的FFT Bin索引
        int lowBin = Mathf.FloorToInt(300f / _freqPerBin);    // 低频：0-300Hz
        int midBin = Mathf.FloorToInt(4000f / _freqPerBin);   // 中频：300Hz-4kHz
        int highBin = _spectrumData.Length;                   // 高频：>4kHz

        // 2. 计算各频段能量总和
        float lowEnergy = SumSpectrum(_spectrumData, 0, lowBin);
        float midEnergy = SumSpectrum(_spectrumData, lowBin, midBin);
        float highEnergy = SumSpectrum(_spectrumData, midBin, highBin);
        float totalEnergy = lowEnergy + midEnergy + highEnergy;

        // 3. 静音处理：平滑过渡到0（避免突变）
        if (totalEnergy < silenceThreshold)
        {
            currentFeatures.lowBandEnergy = ApplyLowPassFilter(0f, ref _lastLowBandEnergy);
            currentFeatures.midBandEnergy = ApplyLowPassFilter(0f, ref _lastMidBandEnergy);
            currentFeatures.highBandEnergy = ApplyLowPassFilter(0f, ref _lastHighBandEnergy);
            return;
        }

        // 4. 归一化+平滑（相对占比）
        float rawLow = lowEnergy / totalEnergy;
        float rawMid = midEnergy / totalEnergy;
        float rawHigh = highEnergy / totalEnergy;

        currentFeatures.lowBandEnergy = ApplyLowPassFilter(rawLow, ref _lastLowBandEnergy);
        currentFeatures.midBandEnergy = ApplyLowPassFilter(rawMid, ref _lastMidBandEnergy);
        currentFeatures.highBandEnergy = ApplyLowPassFilter(rawHigh, ref _lastHighBandEnergy);
    }


    // ==================== 辅助方法 ====================
    /// <summary> 计算RMS（平均响度） </summary>
    private float CalculateRMS(float[] data)
    {
        float sum = 0;
        foreach (float s in data) sum += s * s;
        return Mathf.Sqrt(sum / data.Length);
    }

    /// <summary> 计算峰值响度 </summary>
    private float CalculatePeak(float[] data)
    {
        float peak = 0;
        foreach (float s in data) peak = Mathf.Max(peak, Mathf.Abs(s));
        return peak;
    }

    /// <summary> 响度归一化（-40dB到0dB映射到0-1） </summary>
    private float NormalizeLoudness(float loudness)
    {
        float db = 20 * Mathf.Log10(loudness + Mathf.Epsilon);
        return Mathf.Clamp01(Mathf.InverseLerp(-40f, 0f, db));
    }

    /// <summary> 计算频谱区间和 </summary>
    private float SumSpectrum(float[] spectrum, int start, int end)
    {
        float sum = 0;
        end = Mathf.Min(end, spectrum.Length); // 避免越界
        for (int i = start; i < end; i++) sum += spectrum[i];
        return sum;
    }

    /// <summary> 一阶低通滤波（核心平滑逻辑） </summary>
    private float ApplyLowPassFilter(float currentValue, ref float lastValue)
    {
        // 第一次调用时初始化历史值（避免初始0导致突变）
        if (lastValue == 0f)
        {
            lastValue = currentValue;
            return currentValue;
        }

        // 滤波公式：平衡响应速度与平滑度
        float filteredValue = Mathf.Lerp(lastValue, currentValue, filterSmoothing);
        lastValue = filteredValue;
        return filteredValue;
    }
}
