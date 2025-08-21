# Unity实时麦克风音频分析插件 - MicAudioAnalyzer


## 一、项目介绍
MicAudioAnalyzer 是一个**轻量级Unity实时音频分析工具**，专注于**麦克风输入的特征提取与稳定处理**。它能快速捕获麦克风音频，计算**响度、频谱能量**等核心特征，并通过**一阶低通滤波**优化数据稳定性，适用于：
- 音频可视化（如随音乐跳动的UI/3D物体）
- 互动音乐游戏（如节奏打击、音效联动）
- 语音控制（如通过响度触发动作）
- 实时音效互动（如根据频率调整特效）


## 二、核心功能
| 功能分类       | 具体说明                                                                 |
|----------------|--------------------------------------------------------------------------|
| 🎤 麦克风管理  | 自动检测设备列表、支持手动刷新、一键切换麦克风（封装完整的「停止→重启」流程） |
| 🔊 特征提取    | 归一化平均响度（RMS）、归一化峰值响度、高中低三频能量（0-300Hz/300Hz-4kHz/>4kHz） |
| 📊 平滑处理    | 一阶低通滤波（可调节平滑系数），平衡数据响应速度与稳定性                   |
| ⚙️ 灵活配置    | 支持调整采样率、FFT大小、RMS窗口、静音阈值等参数                         |


## 三、快速开始
### 1. 安装
将 `MicAudioAnalyzer.cs` 导入Unity项目的`Scripts`目录，挂载到任意空物体上（建议命名为`MicAnalyzer`）。


### 2. 配置（Inspector面板）
| 模块           | 参数说明                                                                 | 推荐值       |
|----------------|--------------------------------------------------------------------------|--------------|
| 🎤 麦克风设置  | - **Sample Rate**：麦克风采样率（需设备支持，通常设为44100）<br>- **Max Record Length**：音频缓存时长（秒） | 44100 / 48000   |
| 🔬 分析参数    | - **Fft Size**：FFT大小（自动转为2的幂，影响频谱精度）<br>- **Rms Window**：响度计算窗口（秒，越小响应越快）<br>- **Silence Threshold**：静音阈值（低于此值视为无输入） | 256 / 512 / 1024 |
| 📊 平滑配置    | - **Filter Smoothing**：平滑系数（0~1，值越小越平滑）                     | 0.2~0.5      |


### 3. 启动与测试
1. 进入Play模式，脚本会**自动启动麦克风录制**（控制台输出`✅ 开始录制：[设备名称]`）。
2. 在Inspector面板中查看`Current Features`实时数据（如`Normalized Rms`随音量变化）。


## 四、关键API与使用示例
### 1. 获取实时音频特征
在其他脚本中访问`currentFeatures`即可获取当前音频特征：
```csharp
using UnityEngine;

public class AudioVisualizer : MonoBehaviour
{
    public MicAudioAnalyzer micAnalyzer; // 拖入场景中的MicAnalyzer物体
    public Transform cube;               // 用于可视化的立方体

    void Update()
    {
        if (micAnalyzer == null || !micAnalyzer._isRecording) return;

        // 示例1：根据低频能量缩放立方体
        float scale = 1 + micAnalyzer.currentFeatures.lowBandEnergy * 5;
        cube.localScale = new Vector3(scale, scale, scale);

        // 示例2：根据RMS调整颜色（红色随响度增强）
        float r = micAnalyzer.currentFeatures.normalizedRms;
        cube.GetComponent<Renderer>().material.color = new Color(r, 0.5f, 0.5f);
    }
}
```


### 2. 切换麦克风
通过`SwitchMicrophone`方法切换设备（支持UI按钮触发）：
```csharp
// 示例：点击按钮切换至第2个麦克风（索引1）
public void OnSwitchMicButtonClick()
{
    var analyzer = GetComponent<MicAudioAnalyzer>();
    analyzer.SwitchMicrophone(1); // 索引从0开始，对应麦克风列表顺序
}
```


### 3. 刷新设备列表
手动刷新麦克风列表（如插入新麦克风后）：
```csharp
// 方法1：代码调用
GetComponent<MicAudioAnalyzer>().RefreshMicrophoneDevices();

// 方法2：Inspector面板点击「刷新麦克风列表」按钮（ContextMenu）
```



## 五、注意事项
1. **权限与兼容性**：
   - Unity需获取麦克风权限，WebGL平台需在`Player Settings → WebGL → Publishing Settings`中开启「Microphone」权限。
   - 部分设备可能不支持高采样率（如44100Hz），需调整`Sample Rate`至设备支持的值（可通过系统麦克风设置查看）。
2. **切换麦克风的正确姿势**：
   - 禁止直接修改`selectedMicIndex`（需通过`SwitchMicrophone`方法触发**停止当前录制→切换设备→重启录制**的完整流程）。
3. **数据稳定性优化**：
   - 若特征数据波动过大，可增大`filterSmoothing`（如0.3→0.5）或`rmsWindow`（如0.1→0.2）。
   - 若频谱能量计算不准确，可调整`fftSize`（如256→512，提升频谱精度）。


## 六、常见问题排查
| 问题现象                 | 解决方法                                                                 |
|--------------------------|--------------------------------------------------------------------------|
| 切换麦克风后无声音       | 检查是否通过`SwitchMicrophone`方法切换（而非直接修改索引）；或重新启动录制。 |
| 初始化无设备列表         | 点击「刷新麦克风列表」按钮；检查系统麦克风是否正常连接。                 |
| 数据突然归零（静音）     | 检查`silenceThreshold`是否过小；或麦克风是否被静音。                     |
| 性能占用高               | 降低`fftSize`（如512→256）；或减少`Update`中特征的调用频率。             |


## 七、版本更新记录
- **v1.0**（2025-08-25）：
  - 初始版本，支持麦克风录制、RMS/峰值响度计算、高中低三频能量分析。


## 八、贡献与反馈
欢迎通过 [Issues](https://github.com/your-repo/issues) 提出建议或 Bug 反馈，也可直接提交 Pull Request 改进代码。

如果这个工具对你有帮助，不妨点个 ⭐️ Star 支持一下～ 😊
