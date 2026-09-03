# 测试设备台账界面预览

> 状态：v1.0 已批准界面基线。预览使用合成数据，不包含真实员工、邮箱或资产信息；当前 Razor MVP 已按该基线实现设备操作台。

## 快速查看

- [界面预览总览图](overview.png)
- [可浏览静态原型](index.html)
- [总览页面](overview.html)
- [Razor MVP 桌面实拍](device-desk-desktop.png)
- [Razor MVP 移动实拍](device-desk-mobile.png)
- [UI 对齐后桌面实拍](polished/device-desk-desktop.png)
- [UI 对齐后移动实拍](polished/device-desk-mobile.png)
- [UI 对齐后管理员桌面实拍](polished/admin-devices-desktop.png)
- [UI 对齐后管理员移动实拍](polished/admin-devices-mobile.png)

本地浏览地址：`http://127.0.0.1:4173/docs/ui-preview/`

静态原型通过查询参数切换页面，例如：Razor MVP 可通过仓库 README 中的本地启动命令运行：

- 普通用户设备列表：`?screen=devices&role=user`
- 管理员设备管理：`?screen=admin-devices&role=admin`
- 计划关闭页：`?screen=closed&role=user`

## 视觉基线

- 方向：中性设备台账，优先扫描效率和重复操作效率。
- 桌面：1440×1024，高信息密度表格、单层顶部导航、明确命令入口。
- 移动：360px 宽、800px 首屏视口；长页面采用全页截图，表格转换为分组行且不横向溢出。
- 状态：图标、文字、颜色三重表达；逾期仍属于“借用中”。
- 字体：系统 UI 字体；资产编号、时间和关联 ID 使用等宽字体。
- 色彩：中性灰白工作区、钴蓝主操作，绿/琥珀/红仅用于业务状态。

## 桌面预览

| ID | 页面 | PNG |
| --- | --- | --- |
| D01 | 账户入口 | [查看](screenshots/desktop/d01-auth.png) |
| D02 | 普通用户设备操作台 | [查看](screenshots/desktop/d02-devices.png) |
| D03 | 设备详情 / 本人借用 | [查看](screenshots/desktop/d03-device-detail.png) |
| D04 | 我的借用 | [查看](screenshots/desktop/d04-my-loans.png) |
| D05 | 设备管理 / 归档确认 | [查看](screenshots/desktop/d05-admin-devices.png) |
| D06 | 新增设备 / 图片校验 | [查看](screenshots/desktop/d06-device-form.png) |
| D07 | 借用管理 / 异常账户 | [查看](screenshots/desktop/d07-admin-loans.png) |
| D08 | 续借与强制操作 | [查看](screenshots/desktop/d08-loan-actions.png) |
| D09 | 默认借期设置 | [查看](screenshots/desktop/d09-policy.png) |
| D10 | 审计查询 | [查看](screenshots/desktop/d10-audit.png) |
| D11 | 通知失败 / 人工复核 | [查看](screenshots/desktop/d11-notifications.png) |
| D12 | 19:00 后计划关闭 | [查看](screenshots/desktop/d12-closed.png) |
| D13 | 反馈与异常状态 | [查看](screenshots/desktop/d13-feedback.png) |

## 移动预览

| ID | 页面 | PNG |
| --- | --- | --- |
| M01 | 登录 | [查看](screenshots/mobile/m01-auth.png) |
| M02 | 普通用户设备列表 | [查看](screenshots/mobile/m02-devices.png) |
| M03 | 设备详情 / 本人借用 | [查看](screenshots/mobile/m03-device-detail.png) |
| M04 | 我的借用 | [查看](screenshots/mobile/m04-my-loans.png) |
| M05 | 管理员借用管理 | [查看](screenshots/mobile/m05-admin-loans.png) |
| M06 | 管理员设备录入 | [查看](screenshots/mobile/m06-device-form.png) |
| M07 | 计划关闭页 | [查看](screenshots/mobile/m07-closed.png) |

D08 和 D13 是供评审交互状态的组合板，不是产品中真实存在的单一页面；实际使用时每个弹窗或反馈状态独立出现。

## 评审边界

- 原型中的按钮和筛选器只展示视觉与状态，不连接后端。
- 登录、权限、借用、归还、通知、审计和时间门禁行为仍以需求及开发设计文档为准。
- 业务负责人已确认界面方向；页面结构和交互状态已纳入实施计划，并以本预览作为实现验收基线。

## 第三方资源

图标使用本地固定版本 Lucide 1.37.0，许可证见 [LUCIDE_LICENSE.txt](LUCIDE_LICENSE.txt)。预览不依赖外网字体、图片或脚本。
