# AcFun爱咔下载助手

这是一款通过AcFun爱咔下载 AcFun 直播录像的桌面应用程序。通过二维码登录后，应用可以获取直播详情并保存至本地数据库，还支持直播录像下载（**仅可下载2个月内的直播录像**）。

## 主要功能

- **二维码登录**  
  使用 AcFun APP 扫描二维码进行登录。

- **直播数据获取**  
  自动获取直播详情，并保存至本地数据库。

- **视频下载**  
  根据直播详情下载直播录像视频。  
  **注意**：视频下载功能依赖 [N_m3u8DL-RE](https://github.com/nilaoda/N_m3u8DL-RE) 和 [ffmpeg](https://ffmpeg.org/)。请自行下载这两个工具，并将它们放入项目根目录下的 `Tools` 文件夹中。如果文件不存在，将复制下载链接至剪贴板。

- **历史记录与搜索**  
  可通过用户名或用户 ID 搜索已保存的历史直播记录。

## 使用说明

1. **克隆项目**

   ```
   git clone https://github.com/wpscott/AcFun_Aika_Helper
   cd AcFun_Aika_Helper
   ```

2. **还原依赖**

    ```
    dotnet restore
    ```

3. **准备工具**（可选，如果想使用其他工具下载）

    下载 [N_m3u8DL-RE](https://github.com/nilaoda/N_m3u8DL-RE) 和 [ffmpeg](https://ffmpeg.org/)，将 N_m3u8DL-RE.exe 与 ffmpeg.exe 放入项目根目录下的`Tools`文件夹中。

4. **构建项目**

    ```
    dotnet build
    ```

5. **运行应用**

    ```
    dotnet run
    ```

6. **登录与操作**
    - 启动后，可在**本地**标签页搜索查看历史记录。如果历史记录里的下载链接失效，请复制爱咔号到**在线**标签页获取最新下载链接。
    - 首次打开**在线**标签页会显示 AcFun 的登录二维码，请使用 AcFun APP 扫码完成登录。
    - 登录成功后即可输入爱咔号获取直播详情和下载直播录像。

## 日志与数据库

- **日志**：应用使用 Serilog 输出日志，日志文档会在应用目录下生成。
- **数据库**：直播记录保存在系统应用数据目录下的 AcFun爱咔/app.db 文档中，首次运行时会自动创建数据库。