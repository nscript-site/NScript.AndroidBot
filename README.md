# NScript.AndroidBot

安卓设备软件机器人。控制代码翻译自 scrcpy ，添加了针对 Bot 应用的修改和优化，适合手机投屏，手机控制，APP 数据抓取，RPA 等应用场景。

集成了下面三个开源项目：

- [scrcpy](https://github.com/Genymobile/scrcpy)，可以显示与控制手机，scrcpy 用于转发画面内容及控制；
- [sndcpy](https://github.com/rom1v/sndcpy)，可以转发音频内容；
- [uiautomator2](https://github.com/openatx/uiautomator2)，可以使用 UIAutomator 自动化框架。

## 运行

需要下载 [ffmpeg](https://github.com/nscript-site/NScript.AndroidBot/releases/download/lib/ffmpeg20180408.zip)，解压在 ./lib 目录下，确保通过 ./lib/ffmpeg/ 目录可以找到对应的 dll 文件。

运行时，手机请设置成调试模式，且关闭 “监控ADB安装应用” 选项。

## 使用

典型的使用场景如下：

```csharp
Client = new BotClient();
Client.Options.MaxSize = 1024;          // 修改最大画面
Client.Options.MaxFps = 15;             // 设置最大的 fps
// Client.Options.Serial = "XXXXXXX";   // 设置序列号可以连接特定的设备，否则连接默认设备
                                        // 设备名称列表可通过 adb devices 查看
Client.OnMsg = OnMsg;                   // 监听程序
Client.Slicer.Enable = true;    // 开启切片保存
Client.OnRender = OnRender;             // 如果不需要显示画面，可以不设置 OnRender
Client.OnAudioDataReceive = (data) => { }; // 处理音频数据，如果不播放声音，可以不设置。音频数据为 44100 hz, 单通道 Int32 数据。sndcpy 发送来的数据为大端格式的数据，这里已经转换为小端格式。每一次接受的音频数据量为 44100 * 4 / MaxFps bytes。 
Client.Run();                           // 启动 BotClient
```

运行 adb devices 可以列出机器所连接的安卓设备。每个 BotClient 对应一个安卓机器，通过设置 Client.Options.Serial 可以指定所控制的设备，若不指定 Serial，则连接默认的设备。

Client.Slicer.Enable 默认为 false，如果设为 true，则默认将画面和音频保存在本地，保存时对音视频文件进行自动切片处理。可以设置 Client.Slicer.MaxDuration 的值，来设置每段视频的最长时间。

可通过 GetFameImage 方法获取手机画面

```csharp
var img = Client.GetFameImage();
// do something
```

可通过 GetScreenXml() 方法，通过 UIAutomator 自动化框架返回当前屏幕 UI 组件的 xml 描述，默认会忽略没有文字的组件。GetScreenXml(false) 会返回所有的组件。

```csharp
var page = Client.GetScreenXml();
Console.WriteLine(page.Content);  // xml 内容
Console.WriteLine(page.First("//node[@resource-id='xxxxxx']").OuterXml);  // 查找 resource-id 为 xxxxxx 的第一个 node
Console.WriteLine(page.All("//node[@resource-id='xxxxxx']").Count);  // 查找所有 resource-id 为 xxxxxx 的 nodes 的数量
// do something
```

可以通过 xpath 来查找对应的 nodes。对于每一个 node，BotClient 提供了 GetNodeBounds 方法，返回该 node 在监控画面中的坐标，提供了 SendClick 方法来点击指定的 node。

可以通过 push 向手机发送消息，支持发送的消息有：

- InjectKeycodeMsg
- InjectTextMsg
- InjectTouchEventMsg
- InjectScrollEventMsg
- BackOrScreenOnMsg
- SetClipboardMsg
- SetScreenPowerModeMsg
- ExpandNotificationPanelMsg
- ExpandSettingPanelMsg
- CollapsePanelsMsg
- GetClipboardMsg
- RotateDeviceMsg

直接使用 push 比较繁琐。BotClient 里对于常见的操作进行了封装：

- Send, 发送鼠标事件
- SentClick, 发送点击事件
- SendText, 发送文本
- SendTouchMove, 触摸滑动
- SendBack, 后退
- SendWait，等待
- SendClipboardMsg，发送剪贴板信息

例如，向手机的剪贴板发送文字：

```csharp
SetClipboardMsg msg = new SetClipboardMsg("Hello!", false);
Client.Push(msg);
```