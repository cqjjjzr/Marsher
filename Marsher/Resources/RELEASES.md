# Marsher Updated

Marsher已更新到版本`v1.1.0`。

Marsherはバージョン`v1.1.0`に更新されました。

详情请参阅 [https://soft.danmuji.org/Marsher/README.html](https://soft.danmuji.org/Marsher/README.html)

## 新功能

### 导入与导出（Import & Export，インポート ＆ エクスポート）

将项目从列表中拖动到文件资源管理器（如桌面）可导出这些项目到文件以用于分发、协作等。

将文件拖动到列表区域（不需要预先创建列表），即可进行导入。

## 优化

- 优化了删除项目时候的性能（在不必要的时候不更新UI）。
- 优化了新建、重命名列表时候的UI（在出现无效名称的时候会直接显示，而非弹出新的对话框指出）。

## Bug修复

少量小bug。

## 已知问题及临时解决方案

- 卸载无法正常进行。临时解决：在`C:\Users\<用户名>\AppData\Local\Marsher`中打开命令提示符，执行`.\Update --uninstall`。

## 历史版本

### v1.0.0

第一个版本。