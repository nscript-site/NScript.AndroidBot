# NScript.AndroidBot

安卓设备控制器。翻译自 scrcpy ，添加了针对 Bot 应用的修改和优化。

## 运行

需要下载 [ffmpeg](https://github.com/nscript-site/NScript.AndroidBot/releases/download/lib/ffmpeg20180408.zip)，解压在 ./lib 目录下，确保通过 ./lib/ffmpeg/ 目录可以找到对应的 dll 文件。

运行时，手机请设置成调试模式。

## 使用

常见的使用场景如下：

```csharp
Client = new BotClient();
Client.Options.MaxSize = 1024;          // 修改最大画面
// Client.Options.Serial = "XXXXXXX";   // 设置序列号可以连接特定的设备，否则连接默认设备
                                        // 设备名称列表可通过 adb devices 查看
Client.OnMsg = OnMsg;                   // 监听程序
Client.OnRender = OnRender;             // 如果不需要显示画面，可以不设置 OnRender
Client.Run();                           // 启动 BotClient
```

可通过 GetFameImage 方法获取手机画面

```csharp
var img = Client.GetFameImage();
// do something
```

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

例如，向手机的剪贴板发送文字：

```csharp
SetClipboardMsg msg = new SetClipboardMsg("Hello!", false);
Client.Push(msg);
```