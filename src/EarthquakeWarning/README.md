# ClassIsland 地震预警插件

一个现代化UI的地震预警插件。

## 使用须知

安装后，插件默认不会立即生效。为了确保您能收到预警，请务必执行以下操作：

1. 打开 ClassIsland 设置 → 提醒页。
2. 在提醒提供方中选择 地震预警，通过自动/手动方式配置经纬度与其他设置。
3. 选择 API 源：默认为 MIUI API，无需鉴权即可使用；若选择 Voyage Project API，则需填写由 https://api.odysphere.tech/apply 申请得到的 API Token，否则无法收到预警。

## API 源

插件提供两种 API 源，二者提供的内容完全一致：

- Voyage Project API：由插件作者开发的 API 服务，采用 WebSocket 连接，需要填写 API Token 鉴权。
- MIUI API：采用 POST 请求，无需鉴权，开箱即用。受限于 MIUI 预警源返回内容较多及网络因素，此方式的预警相较于 WebSocket 连接可能存在 1-3 秒延迟。

### API Token 申请（仅 Voyage Project API 需要）

申请地址：https://api.odysphere.tech/apply

填写密码：FengLin_OvO

ClassIsland 用户在申请时，请务必在使用用途栏填写“ClassIsland地震预警插件”，否则将视为普通用户，有概率不受理申请。

### 关于 Voyage Project API

这是一个由插件作者以个人爱好形式开发的 API 服务，您可前往主页（https://api.odysphere.tech/）查看具体内容。

## 示例

### 程序独立UI预警

![程序独立UI预警·倒计时](https://raw.githubusercontent.com/FengLinOvO/VoyageEarthquakeWarningForClassisland/main/src/EarthquakeWarning/Assets/Images/uieew_orange.png)

![程序独立UI预警·已到达](https://raw.githubusercontent.com/FengLinOvO/VoyageEarthquakeWarningForClassisland/main/src/EarthquakeWarning/Assets/Images/uiari_orange.png)

### ClassIsland原生横幅预警

![ClassIsland原生横幅预警·倒计时](https://raw.githubusercontent.com/FengLinOvO/VoyageEarthquakeWarningForClassisland/main/src/EarthquakeWarning/Assets/Images/eew_orange.png)

![ClassIsland原生横幅预警·已到达](https://raw.githubusercontent.com/FengLinOvO/VoyageEarthquakeWarningForClassisland/main/src/EarthquakeWarning/Assets/Images/ari_orange.png)

### 独立错误提示

#### 崩溃性错误

![崩溃性错误](https://raw.githubusercontent.com/FengLinOvO/VoyageEarthquakeWarningForClassisland/main/src/EarthquakeWarning/Assets/Images/bug_ui_2.png)

#### 非崩溃性错误

![非崩溃性错误](https://raw.githubusercontent.com/FengLinOvO/VoyageEarthquakeWarningForClassisland/main/src/EarthquakeWarning/Assets/Images/bug_ui_1.png)

## 功能特点

- 接收中国地震预警网地震预警信息
- 本地预估烈度与震中距计算
- ClassIsland 原生横幅预警
- 插件独立地震预警界面
- 蓝色、黄色、橙色、红色预警分级
- 内置地震预警音频与更新报音频
- 自动定位与手动经纬度
- 模拟预警
- 置顶提醒、强制音量

## 地震预警概况

地震预警信息由中国地震台网中心产出并下发，由 Voyage Project API 与 MIUI API 两种数据源提供。

## 免责声明

- 地震预警信息由中国地震台网中心产出并下发，API由 Voyage Project API 与 MIUI API 提供；作者不对地震预警信息的准确性、有效性等作任何承诺与保证，对于使用本API造成的一切后果与作者无关。
- 本插件仅分发地震预警信息，并非地震预测。
- 作者对因信息延迟、误报或漏报所造成的任何损失不承担法律责任，请以官方发布信息为准。

## 许可证

本仓库采用 LGPL-3.0-only 许可证。
