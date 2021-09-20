# adb 命令参考

## forward 与 reverse 

Android允许我们通过ADB，把Android上的某个端口映射到电脑（adb forward），或者把电脑的某个端口映射到Android系统（adb reverse）

- 必须是在连接数据线usb的前提下才能使用该方案进行代码调试

- (Android 5.0 及以上)使用 adb reverse 命令，这个选项只能在 5.0 以上版本(API 21+)的安卓设备上使用

