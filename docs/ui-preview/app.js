const previewTime = "2026-08-31 16:42";

const devices = [
  {
    id: "NAT-021",
    model: "iPhone 16 Pro",
    tier: "高端",
    tierColor: "#7b5db2",
    color: "#bac7dd",
    status: "available",
    statusLabel: "空闲",
    person: "—",
    due: "可立即借用",
    icon: "circle-check"
  },
  {
    id: "QA-014",
    model: "Galaxy S24 Ultra",
    tier: "高端",
    tierColor: "#7b5db2",
    color: "#c7c0ae",
    status: "borrowed",
    statusLabel: "借用中",
    person: "林乔（本人）",
    due: "明天 16:30 到期",
    icon: "clock-3",
    mine: true
  },
  {
    id: "DEV-037",
    model: "Pixel 9",
    tier: "中端",
    tierColor: "#2e7b78",
    color: "#b8d2c7",
    status: "borrowed",
    statusLabel: "借用中",
    person: "王蕾",
    due: "已逾期 2 小时",
    icon: "clock-alert",
    overdue: true
  },
  {
    id: "LAB-052",
    model: "Redmi Note 14",
    tier: "低端",
    tierColor: "#697386",
    color: "#d2c6bc",
    status: "unavailable",
    statusLabel: "暂不可借",
    person: "—",
    due: "摄像头维修中",
    icon: "circle-slash-2"
  },
  {
    id: "LAB-019",
    model: "iPhone 13 mini",
    tier: "中端",
    tierColor: "#2e7b78",
    color: "#cad0dc",
    status: "available",
    statusLabel: "空闲",
    person: "—",
    due: "可立即借用",
    icon: "circle-check"
  }
];

function icon(name, label = "") {
  const aria = label ? `aria-label="${label}"` : `aria-hidden="true"`;
  return `<i data-lucide="${name}" ${aria}></i>`;
}

function button(label, iconName, kind = "", extra = "") {
  return `<button class="${kind}" type="button" ${extra}>${icon(iconName)}<span>${label}</span></button>`;
}

function statusChip(kind, label, iconName) {
  return `<span class="status ${kind}">${icon(iconName)}<span>${label}</span></span>`;
}

function phoneThumb(device, large = false) {
  return `<div class="phone-thumb${large ? " large" : ""}" style="--phone-color:${device.color}" role="img" aria-label="${device.model} 设备展示图"></div>`;
}

function deviceCell(device) {
  return `<div class="device-cell">
    ${phoneThumb(device)}
    <div style="min-width:0">
      <div class="device-title">${device.model}</div>
      <div class="device-meta">${device.id}</div>
    </div>
  </div>`;
}

function assetTag(device) {
  return `<div class="asset-tag" style="--tier-color:${device.tierColor}">${device.id}<span class="tier-label">${device.tier}</span></div>`;
}

function nav(role, active) {
  const items = role === "admin"
    ? [
        ["devices", "设备", "smartphone"],
        ["my-loans", "我的借用", "briefcase-business"],
        ["admin-devices", "设备管理", "database"],
        ["admin-loans", "借用管理", "clipboard-list"],
        ["policy", "设置", "settings-2"],
        ["audit", "审计", "scroll-text"]
      ]
    : [
        ["devices", "设备", "smartphone"],
        ["my-loans", "我的借用", "briefcase-business"]
      ];

  return items.map(([screen, label, iconName]) => `
    <button class="nav-item ${active === screen ? "active" : ""}" type="button">
      ${icon(iconName)}<span>${label}</span>
    </button>`).join("");
}

function mobileNav(role, active) {
  const items = role === "admin"
    ? [["devices", "设备"], ["my-loans", "我的借用"], ["admin-loans", "管理"]]
    : [["devices", "设备"], ["my-loans", "我的借用"]];
  return `<div class="segmented mobile-only" aria-label="移动端页面导航">
    ${items.map(([screen, label]) => `<button class="segment ${active === screen ? "active" : ""}" type="button">${label}</button>`).join("")}
  </div>`;
}

function shell({ role = "user", active = "devices", title, subtitle, actions = "", content }) {
  const isAdmin = role === "admin";
  return `<div class="app-shell">
    <header class="topbar">
      <a class="brand" href="#" aria-label="测试设备台账首页">
        <span class="brand-mark">${icon("smartphone")}</span>
        <span><span class="brand-name">测试设备台账</span><span class="brand-sub">Device Desk</span></span>
      </a>
      <nav class="nav" aria-label="主导航">${nav(role, active)}</nav>
      <div class="top-actions">
        <div class="availability"><span class="availability-dot"></span><span>开放至 19:00</span></div>
        <button class="user-menu ghost" type="button" aria-label="账户菜单">
          <span class="avatar">${isAdmin ? "陈" : "林"}</span>
          <span class="user-copy"><span class="user-name">${isAdmin ? "陈述" : "林乔"}</span><span class="user-role">${isAdmin ? "测试组管理员" : "普通用户"}</span></span>
          ${icon("chevron-down")}
        </button>
        <button class="icon-button ghost mobile-only" type="button" aria-label="打开导航">${icon("menu")}</button>
      </div>
    </header>
    <main class="main" id="main-content">
      ${mobileNav(role, active)}
      <header class="page-head">
        <div><h1 class="page-title">${title}</h1><p class="page-subtitle">${subtitle}</p></div>
        <div class="page-actions">${actions}</div>
      </header>
      ${content}
    </main>
    <div class="preview-note">界面评审稿 · 合成数据 · ${previewTime} CST</div>
  </div>`;
}

function metrics() {
  const values = [
    ["smartphone", "72", "全部设备"],
    ["circle-check", "41", "空闲"],
    ["clock-3", "26", "借用中"],
    ["circle-slash-2", "5", "暂不可借"]
  ];
  return `<section class="metrics-strip" aria-label="设备状态概览">
    ${values.map(([iconName, value, label]) => `<div class="metric"><span class="metric-icon">${icon(iconName)}</span><span><span class="metric-value">${value}</span><span class="metric-label">${label}</span></span></div>`).join("")}
  </section>`;
}

function deviceToolbar(admin = false) {
  return `<div class="toolbar">
    <label class="search-box">${icon("search")}<span class="sr-only"></span><input aria-label="搜索设备" placeholder="搜索型号或资产编号" value=""></label>
    <div class="segmented" aria-label="状态筛选">
      <button class="segment active" type="button">全部</button>
      <button class="segment" type="button">空闲</button>
      <button class="segment" type="button">借用中</button>
      <button class="segment" type="button">暂不可借</button>
      ${admin ? `<button class="segment" type="button">已归档</button>` : ""}
    </div>
    <select aria-label="设备档位"><option>全部档位</option><option>高端</option><option>中端</option><option>低端</option></select>
    <span class="toolbar-spacer"></span>
    <span class="result-count">共 72 台</span>
  </div>`;
}

function deviceAction(device, admin = false) {
  if (admin) {
    const disabled = device.status === "borrowed" ? "disabled title=\"借用中设备不能直接暂停或归档\"" : "";
    return `<div class="row-actions">
      ${button("编辑", "square-pen", "")}
      ${button(device.status === "unavailable" ? "恢复" : "暂停", device.status === "unavailable" ? "play" : "pause", "", disabled)}
      ${button("归档", "archive", "ghost", disabled)}
    </div>`;
  }
  if (device.status === "available") return button("借用", "hand", "primary");
  if (device.mine) return button("归还", "undo-2", "");
  return `<span class="secondary-line">暂无可用操作</span>`;
}

function deviceRows(admin = false) {
  return devices.map((device) => {
    const statusLabel = device.overdue
      ? `${statusChip("borrowed", "借用中", "clock-3")}<div style="margin-top:5px">${statusChip("overdue", "逾期", "triangle-alert")}</div>`
      : statusChip(device.status, device.statusLabel, device.icon);
    return `<tr>
      <td>${deviceCell(device)}</td>
      <td>${assetTag(device)}</td>
      <td>${statusLabel}</td>
      <td><div class="primary-line">${device.person}</div><div class="secondary-line">${device.due}</div></td>
      <td><div class="row-actions">${deviceAction(device, admin)}</div></td>
    </tr>`;
  }).join("");
}

function deviceTable(admin = false) {
  return `<div class="table-wrap responsive-table">
    <table aria-label="设备列表">
      <thead><tr>
        <th style="width:30%">设备</th>
        <th style="width:16%">资产标签</th>
        <th style="width:17%">状态</th>
        <th style="width:23%">借用信息</th>
        <th style="width:14%;text-align:right">操作</th>
      </tr></thead>
      <tbody>${deviceRows(admin)}</tbody>
    </table>
  </div>${mobileDeviceList(admin)}`;
}

function mobileDeviceList(admin = false) {
  return `<div class="mobile-list" aria-label="移动端设备列表">${devices.map((device) => `
    <div class="mobile-device-row" style="--tier-color:${device.tierColor}">
      ${phoneThumb(device)}
      <div style="min-width:0">
        <div class="device-title">${device.model}</div>
        <div class="device-meta">${device.id} · ${device.tier}</div>
        <div class="mobile-status-line">
          ${statusChip(device.status, device.statusLabel, device.icon)}
          ${device.overdue ? statusChip("overdue", "逾期", "triangle-alert") : ""}
          <span class="secondary-line" style="margin:0">${device.person !== "—" ? `${device.person} · ` : ""}${device.due}</span>
        </div>
      </div>
      <div>${admin ? button("管理", "ellipsis", "icon-button", "aria-label=\"管理设备\"") : deviceAction(device, false)}</div>
    </div>`).join("")}</div>`;
}

function renderAuth() {
  return `<div class="auth-page">
    <section class="auth-context" aria-label="设备状态摘要">
      <a class="brand" href="#">
        <span class="brand-mark">${icon("smartphone")}</span>
        <span><span class="brand-name">测试设备台账</span><span class="brand-sub">Device Desk</span></span>
      </a>
      <div class="auth-snapshot">
        <div class="auth-snapshot-label">内部系统 · 测试组设备</div>
        <div class="auth-rack" aria-label="测试设备资产标签示意">
          <div class="rack-device">${phoneThumb(devices[0], true)}<span class="rack-tag">NAT-021</span></div>
          <div class="rack-device">${phoneThumb(devices[2], true)}<span class="rack-tag">DEV-037</span></div>
          <div class="rack-device">${phoneThumb(devices[3], true)}<span class="rack-tag">LAB-052</span></div>
        </div>
      </div>
      <div class="auth-foot">仅限公司邮箱 · 每日 09:00–19:00 开放</div>
    </section>
    <main class="auth-form-area" id="main-content">
      <form class="auth-form">
        <div class="segmented" aria-label="账户操作" style="margin-bottom:24px;width:100%">
          <button class="segment active" type="button">登录</button>
          <button class="segment" type="button">注册</button>
          <button class="segment" type="button">找回密码</button>
        </div>
        <h1>登录设备台账</h1>
        <div class="auth-form-sub">使用已验证的公司邮箱进入系统。</div>
        <div class="alert info" style="margin-bottom:18px">
          ${icon("mail-check")}<div><div class="alert-title">验证邮件已发送</div><div class="alert-copy">请在 24 小时内完成验证。60 秒后可重新发送。</div></div>
        </div>
        <div class="field"><label for="email">公司邮箱</label><input id="email" type="email" autocomplete="email" value="lin.qiao@example.corp"></div>
        <div class="field"><label for="password">密码</label><input id="password" type="password" autocomplete="current-password" value="preview-password"></div>
        <div class="field-error">${icon("circle-alert")}邮箱或密码不正确，请重新输入。</div>
        <button class="primary" type="button">${icon("log-in")}登录</button>
        <div class="auth-links"><a href="#">注册账号</a><a href="#">忘记密码</a></div>
        <div class="auth-open"><span class="availability-dot"></span><span>当前开放，今天 19:00 关闭</span></div>
      </form>
    </main>
  </div><div class="preview-note">D01 / M01 · 账户入口</div>`;
}

function renderDevices(role = "user") {
  const admin = role === "admin";
  const content = `${metrics()}${deviceToolbar(admin)}${deviceTable(admin)}`;
  return shell({
    role,
    active: admin ? "admin-devices" : "devices",
    title: admin ? "设备管理" : "设备",
    subtitle: admin ? "维护设备资料、可借状态与归档记录。" : "查看测试组设备状态并借用空闲设备。",
    actions: admin ? button("新增设备", "plus", "primary") : button("刷新", "refresh-cw", "hide-mobile"),
    content
  });
}

function renderDeviceDetail(role = "user") {
  const d = devices[1];
  const content = `<div class="panel">
    <div class="device-detail-layout">
      <div class="device-visual">${phoneThumb(d, true)}</div>
      <section class="detail-info">
        <div class="detail-kicker">${d.id} · ${d.tier}</div>
        <h2>${d.model}</h2>
        ${statusChip("borrowed", "借用中", "clock-3")}
        <dl class="definition-grid">
          <div class="definition"><dt>品牌</dt><dd>Samsung</dd></div>
          <div class="definition"><dt>操作系统</dt><dd>Android 15</dd></div>
          <div class="definition"><dt>内存 / 存储</dt><dd>12 GB / 256 GB</dd></div>
          <div class="definition"><dt>存放位置</dt><dd>测试组 A 柜 03</dd></div>
        </dl>
        <div class="loan-summary">
          <div class="loan-summary-head"><strong>当前由你借用</strong>${statusChip("neutral", "剩余 23 小时", "hourglass")}</div>
          <div class="loan-summary-grid">
            <div><div class="secondary-line">借用人</div><div class="primary-line">林乔</div></div>
            <div><div class="secondary-line">借出时间</div><div class="primary-line data">08-31 16:30</div></div>
            <div><div class="secondary-line">到期时间</div><div class="primary-line data">09-01 16:30</div></div>
          </div>
          <div style="display:flex;justify-content:flex-end;margin-top:16px">${button("归还设备", "undo-2", "primary")}</div>
        </div>
      </section>
    </div>
  </div>`;
  return shell({
    role,
    active: "devices",
    title: "设备详情",
    subtitle: "Galaxy S24 Ultra · QA-014",
    actions: button("返回设备列表", "arrow-left", "hide-mobile"),
    content
  });
}

function renderMyLoans(role = "user") {
  const content = `<div class="tabs" role="tablist">
    <button class="tab active" role="tab" type="button">当前借用 <span class="status neutral" style="margin-left:6px">2</span></button>
    <button class="tab" role="tab" type="button">历史记录</button>
  </div>
  <div class="alert warning" style="margin-bottom:14px">
    ${icon("triangle-alert")}<div><div class="alert-title">有 1 台设备已逾期</div><div class="alert-copy">请尽快归还 Pixel 9，或联系测试组管理员处理续借。</div></div>
  </div>
  <div class="table-wrap responsive-table">
    <table aria-label="我的借用记录">
      <thead><tr><th style="width:31%">设备</th><th style="width:17%">状态</th><th style="width:18%">借出时间</th><th style="width:20%">到期 / 归还</th><th style="width:14%;text-align:right">操作</th></tr></thead>
      <tbody>
        <tr><td>${deviceCell(devices[1])}</td><td>${statusChip("borrowed", "借用中", "clock-3")}</td><td class="data">08-31 16:30</td><td><div class="data">09-01 16:30</div><div class="secondary-line">剩余 23 小时</div></td><td><div class="row-actions">${button("归还", "undo-2", "")}</div></td></tr>
        <tr><td>${deviceCell(devices[2])}</td><td>${statusChip("overdue", "逾期", "triangle-alert")}</td><td class="data">08-30 14:40</td><td><div class="data" style="color:var(--danger)">08-31 14:40</div><div class="secondary-line">已逾期 2 小时</div></td><td><div class="row-actions">${button("归还", "undo-2", "primary")}</div></td></tr>
        <tr><td>${deviceCell(devices[4])}</td><td>${statusChip("neutral", "已归还", "check")}</td><td class="data">08-27 10:15</td><td><div class="data">08-27 18:06</div><div class="secondary-line">本人归还</div></td><td><div class="row-actions">${button("查看", "eye", "ghost")}</div></td></tr>
      </tbody>
    </table>
  </div>
  <div class="mobile-list">
    ${[devices[1], devices[2], devices[4]].map((d, index) => `<div class="mobile-device-row" style="--tier-color:${d.tierColor}">${phoneThumb(d)}<div><div class="device-title">${d.model}</div><div class="device-meta">${d.id}</div><div class="mobile-status-line">${index === 1 ? statusChip("overdue", "逾期", "triangle-alert") : index === 2 ? statusChip("neutral", "已归还", "check") : statusChip("borrowed", "借用中", "clock-3")}<span class="secondary-line" style="margin:0">${index === 1 ? "已逾期 2 小时" : index === 2 ? "08-27 18:06 归还" : "09-01 16:30 到期"}</span></div></div>${index < 2 ? button("归还", "undo-2", index === 1 ? "primary" : "") : button("查看", "eye", "icon-button", "aria-label=\"查看借用记录\"")}</div>`).join("")}
  </div>`;
  return shell({ role, active: "my-loans", title: "我的借用", subtitle: "跟踪当前借用、到期时间和历史归还记录。", content });
}

function archiveDialog() {
  const d = devices[0];
  return `<div class="modal-layer" role="presentation">
    <section class="dialog" role="dialog" aria-modal="true" aria-labelledby="archive-title">
      <header class="dialog-head"><div class="dialog-title" id="archive-title">归档设备</div>${button("关闭", "x", "icon-button ghost", "aria-label=\"关闭对话框\"")}</header>
      <div class="dialog-body">
        <div class="alert warning" style="margin-bottom:16px">${icon("archive")}<div><div class="alert-title">归档后普通设备列表将不再显示</div><div class="alert-copy">历史借用与审计记录仍会保留。存在未归还借用时无法归档。</div></div></div>
        <div class="dialog-device">${phoneThumb(d)}<div><div class="device-title">${d.model}</div><div class="device-meta">${d.id} · 当前空闲</div></div></div>
        <div class="field"><label for="archive-reason">归档原因 <span class="required">*</span></label><textarea id="archive-reason">设备已转入兼容性留档，不再参与日常借用。</textarea></div>
      </div>
      <footer class="dialog-footer">${button("取消", "x", "")}${button("确认归档", "archive", "danger")}</footer>
    </section>
  </div>`;
}

function renderAdminDevices() {
  return `${renderDevices("admin")}${archiveDialog()}`;
}

function renderDeviceForm() {
  const content = `<form class="form-layout">
    <section class="panel">
      <div class="form-section">
        <div class="form-section-title">基本信息</div>
        <div class="form-grid">
          <div class="field"><label>资产编号 <span class="required">*</span></label><input value="QA-068"></div>
          <div class="field"><label>型号名称 <span class="required">*</span></label><input value="OnePlus 13"></div>
          <div class="field"><label>档位 <span class="required">*</span></label><select><option>高端</option><option>中端</option><option>低端</option></select></div>
          <div class="field"><label>品牌</label><input value="OnePlus"></div>
          <div class="field"><label>操作系统</label><input value="Android 15"></div>
          <div class="field"><label>内存</label><input value="12 GB"></div>
          <div class="field"><label>存储</label><input value="256 GB"></div>
          <div class="field"><label>存放位置</label><input value="测试组 B 柜 06"></div>
        </div>
      </div>
      <div class="form-section">
        <div class="form-section-title">管理员资产信息</div>
        <div class="form-grid">
          <div class="field"><label>序列号</label><input value="SN-2026-0831-68"></div>
          <div class="field invalid"><label>IMEI</label><input value="8659037"><div class="field-error">${icon("circle-alert")}IMEI 应为 15 位数字。</div></div>
          <div class="field full"><label>管理员备注</label><textarea>新购入设备，优先用于支付兼容性回归。</textarea><div class="field-help">该内容仅测试组管理员可见。</div></div>
        </div>
      </div>
      <footer class="form-footer">${button("取消", "x", "")}${button("保存设备", "save", "primary")}</footer>
    </section>
    <aside class="panel">
      <div class="panel-head"><div class="panel-title">设备主图 <span class="required">*</span></div></div>
      <div class="panel-body">
        <div class="upload invalid">
          <span class="upload-icon">${icon("image-up")}</span>
          <div class="upload-title">上传设备展示图</div>
          <div class="upload-copy">JPG、PNG 或 WebP<br>最大 5 MB · 最长边 4096px · 16MP</div>
          <button type="button" style="margin-top:14px">${icon("folder-open")}选择图片</button>
        </div>
        <div class="field-error" style="margin-top:9px">${icon("circle-alert")}当前图片为 21MP，请压缩后重新上传。</div>
      </div>
    </aside>
  </form>`;
  return shell({ role: "admin", active: "admin-devices", title: "新增设备", subtitle: "录入设备资料与管理员资产信息。", actions: button("返回设备管理", "arrow-left", "hide-mobile"), content });
}

function renderAdminLoans() {
  const content = `<div class="tabs"><button class="tab active" type="button">当前借用 26</button><button class="tab" type="button">逾期 3</button><button class="tab" type="button">已归还</button><button class="tab" type="button">异常 1</button></div>
  <div class="toolbar">
    <label class="search-box">${icon("search")}<input aria-label="搜索借用" placeholder="搜索设备、借用人或邮箱"></label>
    <select><option>全部状态</option><option>有效</option><option>逾期</option><option>借用人已停用</option></select>
    <span class="toolbar-spacer"></span><span class="result-count">26 条未归还记录</span>
  </div>
  <div class="table-wrap responsive-table"><table aria-label="管理员借用管理">
    <thead><tr><th style="width:25%">设备</th><th style="width:21%">借用人</th><th style="width:18%">借出时间</th><th style="width:18%">到期状态</th><th style="width:18%;text-align:right">操作</th></tr></thead>
    <tbody>
      <tr><td>${deviceCell(devices[1])}</td><td><div class="primary-line">林乔（管理员本人）</div><div class="secondary-line">lin.q***@example.corp</div></td><td class="data">08-31 16:30</td><td>${statusChip("borrowed", "借用中", "clock-3")}<div class="secondary-line">09-01 16:30</div></td><td><div class="row-actions">${button("本人归还", "undo-2", "")}</div></td></tr>
      <tr><td>${deviceCell(devices[2])}</td><td><div class="primary-line">王蕾</div><div class="secondary-line">wang.l***@example.corp</div></td><td class="data">08-30 14:40</td><td>${statusChip("overdue", "逾期 2 小时", "triangle-alert")}<div class="secondary-line">08-31 14:40</div></td><td><div class="row-actions">${button("续借", "calendar-plus", "")}${button("处理", "ellipsis", "primary")}</div></td></tr>
      <tr><td>${deviceCell(devices[4])}</td><td><div class="primary-line">赵青</div><div class="secondary-line">zhao.q***@example.corp</div><div style="margin-top:5px">${statusChip("failed", "账户已停用", "user-x")}</div></td><td class="data">08-31 09:18</td><td>${statusChip("borrowed", "借用中", "clock-3")}<div class="secondary-line">09-01 09:18</div></td><td><div class="row-actions">${button("处理异常", "shield-alert", "primary")}</div></td></tr>
    </tbody>
  </table></div>
  <div class="mobile-list">
    <div class="mobile-device-row" style="--tier-color:${devices[1].tierColor}">${phoneThumb(devices[1])}<div><div class="device-title">${devices[1].model}</div><div class="device-meta">${devices[1].id}</div><div class="mobile-status-line">${statusChip("borrowed", "借用中", "clock-3")}<span class="secondary-line" style="margin:0">林乔（管理员本人） · 09-01 16:30</span></div></div>${button("归还", "undo-2", "")}</div>
    <div class="mobile-device-row" style="--tier-color:${devices[2].tierColor}">${phoneThumb(devices[2])}<div><div class="device-title">${devices[2].model}</div><div class="device-meta">${devices[2].id}</div><div class="mobile-status-line">${statusChip("overdue", "逾期", "triangle-alert")}<span class="secondary-line" style="margin:0">王蕾 · 逾期 2 小时</span></div></div>${button("处理", "ellipsis", "primary")}</div>
    <div class="mobile-device-row" style="--tier-color:${devices[4].tierColor}">${phoneThumb(devices[4])}<div><div class="device-title">${devices[4].model}</div><div class="device-meta">${devices[4].id}</div><div class="mobile-status-line">${statusChip("failed", "借用人已停用", "user-x")}<span class="secondary-line" style="margin:0">赵青 · 09-01 09:18 到期</span></div></div>${button("处理", "ellipsis", "primary")}</div>
  </div>`;
  return shell({ role: "admin", active: "admin-loans", title: "借用管理", subtitle: "处理当前借用、逾期和账户异常记录。", content });
}

function actionDialog(type) {
  const configs = {
    extend: ["续借", "calendar-plus", "确认续借", "primary"],
    return: ["强制归还", "undo-2", "确认强制归还", "danger"],
    disable: ["强制归还并暂停", "circle-slash-2", "归还并暂停", "danger"]
  };
  const [title, iconName, action, kind] = configs[type];
  const d = devices[2];
  const body = type === "extend" ? `
    <div class="dialog-grid">
      <div class="field"><label>当前到期</label><input value="2026-08-31 14:40" disabled></div>
      <div class="field"><label>服务器时间</label><input value="2026-08-31 16:42" disabled></div>
      <div class="field"><label>增加时长 <span class="required">*</span></label><select><option>24 小时</option><option>2 小时</option><option>3 天</option></select></div>
      <div class="field"><label>新的到期时间</label><input value="2026-09-01 16:42" disabled></div>
      <div class="field full"><label>续借原因 <span class="required">*</span></label><textarea>回归测试尚未完成，批准延长一天。</textarea><div class="field-help">最长不得晚于当前操作时间后 7 天。</div></div>
    </div>` : `
    <div class="alert ${type === "disable" ? "warning" : "danger"}" style="margin-bottom:14px">${icon(type === "disable" ? "circle-slash-2" : "triangle-alert")}<div><div class="alert-title">${type === "disable" ? "设备将直接变为暂不可借" : "当前借用记录将立即关闭"}</div><div class="alert-copy">${type === "disable" ? "过程不会出现短暂空闲，其他用户无法抢借。" : "原借用人会收到包含原因的通知。"}</div></div></div>
    <div class="field"><label>操作原因 <span class="required">*</span></label><textarea>${type === "disable" ? "设备出现触屏故障，收回后暂停借用并送检。" : "借用人已确认设备交回测试组。"}</textarea></div>`;
  return `<section class="dialog" role="dialog" aria-label="${title}预览">
    <header class="dialog-head"><div class="dialog-title">${icon(iconName)} ${title}</div>${icon("x")}</header>
    <div class="dialog-body"><div class="dialog-device">${phoneThumb(d)}<div><div class="device-title">${d.model}</div><div class="device-meta">${d.id} · 王蕾</div></div></div>${body}</div>
    <footer class="dialog-footer">${button("取消", "x", "")}${button(action, iconName, kind)}</footer>
  </section>`;
}

function renderLoanActions() {
  const content = `<div class="alert info" style="margin-bottom:16px">${icon("info")}<div><div class="alert-title">借用操作状态板</div><div class="alert-copy">三个弹窗分别展示续借、强制归还，以及无空闲窗口的强制归还并暂停。</div></div></div>
  <div class="modal-board">${actionDialog("extend")}${actionDialog("return")}${actionDialog("disable")}</div>`;
  return shell({ role: "admin", active: "admin-loans", title: "借用操作", subtitle: "Galaxy S24 Ultra · QA-014", actions: button("返回借用管理", "arrow-left", "hide-mobile"), content });
}

function renderPolicy() {
  const content = `<div class="two-column">
    <section class="panel">
      <div class="panel-head"><div class="panel-title">默认借期</div>${statusChip("available", "策略 v4 生效中", "circle-check")}</div>
      <div class="panel-body">
        <div class="policy-current"><div><div class="policy-number">24</div><div class="policy-unit">小时 · 1,440 分钟</div></div><div><div class="primary-line">当前默认策略</div><div class="secondary-line">自 2026-08-01 09:00 生效</div></div></div>
        <div class="alert info" style="margin-bottom:18px">${icon("info")}<div><div class="alert-title">只影响新借用</div><div class="alert-copy">修改不会追溯当前未归还的借用记录。</div></div></div>
        <div class="form-grid">
          <div class="field"><label>新的默认时长 <span class="required">*</span></label><input type="number" value="24" min="1" max="168"></div>
          <div class="field"><label>单位</label><select><option>小时</option><option>天</option><option>分钟</option></select></div>
          <div class="field full"><label>修改原因 <span class="required">*</span></label><textarea>保持测试设备默认一天归还策略。</textarea><div class="field-help">允许范围：60 分钟至 7 天。</div></div>
        </div>
      </div>
      <footer class="form-footer">${button("保存新版本", "save", "primary")}</footer>
    </section>
    <section class="panel">
      <div class="panel-head"><div class="panel-title">版本历史</div></div>
      <div class="panel-body" style="padding:0">
        <table><thead><tr><th>版本</th><th>时长</th><th>生效时间</th></tr></thead><tbody>
          <tr><td class="data">v4</td><td>24 小时</td><td class="data">08-01 09:00</td></tr>
          <tr><td class="data">v3</td><td>12 小时</td><td class="data">07-15 09:00</td></tr>
          <tr><td class="data">v2</td><td>24 小时</td><td class="data">06-01 09:00</td></tr>
        </tbody></table>
      </div>
    </section>
  </div>`;
  return shell({ role: "admin", active: "policy", title: "借用设置", subtitle: "管理新借用记录使用的默认期限。", content });
}

function renderAudit() {
  const content = `<div class="toolbar">
    <select aria-label="时间范围"><option>今天</option><option>最近 7 天</option></select><select aria-label="操作者"><option>全部操作者</option><option>陈述</option><option>林乔</option></select><select aria-label="事件类型"><option>全部事件</option><option>设备变更</option><option>借用操作</option><option>权限变更</option></select>
    <label class="search-box">${icon("search")}<input aria-label="搜索审计对象" placeholder="搜索资产编号或关联 ID"></label><span class="toolbar-spacer"></span>${button("刷新", "refresh-cw", "")}
  </div>
  <div class="table-wrap responsive-table"><table aria-label="审计事件">
    <thead><tr><th style="width:15%">时间</th><th style="width:16%">操作者</th><th style="width:19%">事件</th><th style="width:20%">对象</th><th style="width:30%">变更与原因</th></tr></thead>
    <tbody>
      <tr><td class="data">16:35:21</td><td><div class="primary-line">陈述</div><div class="secondary-line">测试组管理员</div></td><td>${statusChip("neutral", "续借", "calendar-plus")}</td><td><div class="primary-line">Pixel 9</div><div class="secondary-line data">DEV-037 · LOAN-8842</div></td><td><div class="audit-change"><div class="change-value data">08-31 14:40</div>${icon("arrow-right")}<div class="change-value data">09-01 16:42</div></div><div class="secondary-line">原因：回归测试尚未完成</div></td></tr>
      <tr><td class="data">16:22:08</td><td><div class="primary-line">陈述</div><div class="secondary-line">测试组管理员</div></td><td>${statusChip("unavailable", "暂停借用", "circle-slash-2")}</td><td><div class="primary-line">Redmi Note 14</div><div class="secondary-line data">LAB-052</div></td><td><div class="primary-line">NORMAL → TEMP_DISABLED</div><div class="secondary-line">原因：摄像头维修中</div></td></tr>
      <tr><td class="data">16:01:44</td><td><div class="primary-line">林乔</div><div class="secondary-line">普通用户</div></td><td>${statusChip("available", "借用成功", "hand")}</td><td><div class="primary-line">Galaxy S24 Ultra</div><div class="secondary-line data">QA-014 · LOAN-8841</div></td><td><div class="primary-line">空闲 → 借用中</div><div class="secondary-line code">corr_01K46T8J4S5X</div></td></tr>
    </tbody>
  </table></div>
  <div class="mobile-list mobile-record-list" aria-label="移动端审计事件">
    <article class="mobile-record"><div class="mobile-record-head"><span class="data">16:35:21</span>${statusChip("neutral", "续借", "calendar-plus")}</div><div class="primary-line">Pixel 9 · DEV-037</div><div class="secondary-line">陈述 · 测试组管理员</div><div class="mobile-record-change"><span class="data">08-31 14:40</span>${icon("arrow-right")}<span class="data">09-01 16:42</span></div><div class="secondary-line">原因：回归测试尚未完成</div></article>
    <article class="mobile-record"><div class="mobile-record-head"><span class="data">16:22:08</span>${statusChip("unavailable", "暂停借用", "circle-slash-2")}</div><div class="primary-line">Redmi Note 14 · LAB-052</div><div class="secondary-line">陈述 · 测试组管理员</div><div class="mobile-record-change"><span class="data">NORMAL</span>${icon("arrow-right")}<span class="data">TEMP_DISABLED</span></div><div class="secondary-line">原因：摄像头维修中</div></article>
    <article class="mobile-record"><div class="mobile-record-head"><span class="data">16:01:44</span>${statusChip("available", "借用成功", "hand")}</div><div class="primary-line">Galaxy S24 Ultra · QA-014</div><div class="secondary-line">林乔 · 普通用户</div><div class="secondary-line code">corr_01K46T8J4S5X</div></article>
  </div>`;
  return shell({ role: "admin", active: "audit", title: "审计记录", subtitle: "查看不可变的设备、借用与权限变更记录。", content });
}

function renderNotifications() {
  const content = `<div class="tabs"><button class="tab active" type="button">最终失败 2</button><button class="tab" type="button">待人工复核 1</button><button class="tab" type="button">已处理</button></div>
  <div class="alert warning" style="margin-bottom:14px">${icon("shield-alert")}<div><div class="alert-title">待人工复核的邮件不会自动重发</div><div class="alert-copy">SMTP 接受结果不确定，管理员需先核对后再决定下一步。</div></div></div>
  <div class="table-wrap responsive-table"><table aria-label="通知失败列表">
    <thead><tr><th style="width:16%">状态</th><th style="width:18%">事件</th><th style="width:22%">关联对象</th><th style="width:18%">收件地址</th><th style="width:14%">最后处理</th><th style="width:12%;text-align:right">操作</th></tr></thead>
    <tbody>
      <tr><td>${statusChip("review", "待人工复核", "scan-search")}</td><td><div class="primary-line">到期提醒</div><div class="secondary-line">尝试 1 次</div></td><td><div class="primary-line">Galaxy S24 Ultra</div><div class="secondary-line data">QA-014 · LOAN-8841</div></td><td class="data">li***@example.corp</td><td><div class="data">16:31:10</div><div class="secondary-line">SMTP 响应丢失</div></td><td><div class="row-actions">${button("复核", "search-check", "primary")}</div></td></tr>
      <tr><td>${statusChip("failed", "最终失败", "circle-x")}</td><td><div class="primary-line">邮箱验证</div><div class="secondary-line">尝试 5 次</div></td><td><div class="primary-line">账户验证</div><div class="secondary-line code">evt_01K46R9C</div></td><td class="data">zh***@example.corp</td><td><div class="data">15:48:02</div><div class="secondary-line">Mailbox unavailable</div></td><td><div class="row-actions">${button("查看", "eye", "")}</div></td></tr>
    </tbody>
  </table></div>
  <div class="mobile-list mobile-record-list" aria-label="移动端通知失败列表">
    <article class="mobile-record"><div class="mobile-record-head">${statusChip("review", "待人工复核", "scan-search")}${button("复核", "search-check", "primary")}</div><div class="primary-line">到期提醒 · Galaxy S24 Ultra</div><div class="secondary-line data">QA-014 · LOAN-8841</div><div class="secondary-line">li***@example.corp · 16:31:10</div><div class="secondary-line">SMTP 响应丢失 · 尝试 1 次</div></article>
    <article class="mobile-record"><div class="mobile-record-head">${statusChip("failed", "最终失败", "circle-x")}${button("查看", "eye", "")}</div><div class="primary-line">邮箱验证 · 账户验证</div><div class="secondary-line code">evt_01K46R9C</div><div class="secondary-line">zh***@example.corp · 15:48:02</div><div class="secondary-line">Mailbox unavailable · 尝试 5 次</div></article>
  </div>`;
  return shell({ role: "admin", active: "policy", title: "通知处理", subtitle: "处理最终失败和发送结果不确定的邮件事件。", content });
}

function renderClosed() {
  return `<div class="closed-page">
    <header class="closed-top"><a class="brand" href="#"><span class="brand-mark">${icon("smartphone")}</span><span><span class="brand-name">测试设备台账</span><span class="brand-sub">Device Desk</span></span></a></header>
    <main class="closed-content" id="main-content">
      <div class="closed-inner">
        <div class="clock-face" aria-hidden="true"><span class="clock-dot"></span></div>
        <section>
          <div class="closed-code">503 · OUTSIDE_ACCESS_WINDOW</div>
          <h1 class="closed-title">今天的借用时段已结束</h1>
          <p class="closed-copy">设备台账每天 09:00–19:00 开放。关闭期间注册、登录、查询及管理操作均不可用。</p>
          <div class="reopen-time"><div class="reopen-label">下次开放（上海时间）</div><div class="reopen-value">2026-09-01 09:00</div></div>
          <div style="margin-top:20px;color:var(--ink-faint);font-size:12px">当前时间：2026-08-31 19:16:42 CST</div>
        </section>
      </div>
    </main>
    <div class="preview-note">D12 / M07 · 计划关闭页</div>
  </div>`;
}

function feedbackItem(kind, iconName, title, copy, actions) {
  return `<section class="feedback-item"><div><div class="alert ${kind}" style="border:0;padding:0;background:transparent">${icon(iconName)}<div><div class="alert-title">${title}</div><div class="alert-copy">${copy}</div></div></div></div><div class="feedback-actions">${actions}</div></section>`;
}

function renderFeedback() {
  const content = `<div class="feedback-grid">
    ${feedbackItem("danger", "refresh-cw-off", "设备刚被其他人借走", "当前状态已刷新。请选择其他空闲设备。", button("返回设备列表", "arrow-left", ""))}
    ${feedbackItem("warning", "git-compare-arrows", "设备资料已被更新", "陈述在 16:40 保存了新版本，请重新加载后核对。", button("重新加载", "refresh-cw", "primary"))}
    ${feedbackItem("danger", "log-out", "登录状态已失效", "账户权限已变化，请重新登录后继续。", button("重新登录", "log-in", "primary"))}
    ${feedbackItem("warning", "timer-reset", "请求过于频繁", "请在 14 分 32 秒后再次尝试登录。", button("返回登录", "arrow-left", ""))}
    ${feedbackItem("danger", "shield-x", "没有操作权限", "普通用户不能暂停、归档或修改借用期限。", button("返回可访问页面", "home", ""))}
    ${feedbackItem("info", "circle-help", "暂时无法完成操作", "请稍后重试。联系支持时提供关联 ID：corr_01K46W8R9A。", `${button("重试", "refresh-cw", "primary")}${button("复制关联 ID", "copy", "")}`)}
  </div>`;
  return shell({ role: "admin", active: "admin-devices", title: "反馈与异常状态", subtitle: "关键失败场景使用明确恢复动作和关联信息。", content });
}

function renderForbidden() {
  const content = `<div class="panel"><div class="panel-body"><div class="alert danger">${icon("shield-x")}<div><div class="alert-title">没有操作权限</div><div class="alert-copy">普通用户不能访问测试组管理页面。</div></div></div><div style="margin-top:16px">${button("返回设备列表", "arrow-left", "primary")}</div></div></div>`;
  return shell({ role: "user", active: "devices", title: "访问受限", subtitle: "当前账号没有该页面的访问权限。", content });
}

const params = new URLSearchParams(window.location.search);
const screen = params.get("screen") || "devices";
const role = params.get("role") || "user";
const adminOnlyScreens = new Set(["admin-devices", "device-form", "admin-loans", "loan-actions", "policy", "audit", "notifications", "feedback"]);

const renderers = {
  auth: () => renderAuth(),
  devices: () => renderDevices(role),
  "device-detail": () => renderDeviceDetail(role),
  "my-loans": () => renderMyLoans(role),
  "admin-devices": () => renderAdminDevices(),
  "device-form": () => renderDeviceForm(),
  "admin-loans": () => renderAdminLoans(),
  "loan-actions": () => renderLoanActions(),
  policy: () => renderPolicy(),
  audit: () => renderAudit(),
  notifications: () => renderNotifications(),
  closed: () => renderClosed(),
  feedback: () => renderFeedback()
};

const selectedRenderer = adminOnlyScreens.has(screen) && role !== "admin" ? renderForbidden : (renderers[screen] || renderers.devices);
document.getElementById("app").innerHTML = selectedRenderer();
document.body.dataset.previewScreen = screen;

document.querySelectorAll(".field").forEach((field, index) => {
  const label = field.querySelector("label");
  const control = field.querySelector("input, select, textarea");
  if (label && control && !control.hasAttribute("aria-label")) {
    control.setAttribute("aria-label", label.textContent.replace("*", "").trim());
  }
  if (label?.querySelector(".required") && control) {
    control.required = true;
    control.setAttribute("aria-required", "true");
  }
  if (field.classList.contains("invalid") && control) {
    control.setAttribute("aria-invalid", "true");
    const error = field.querySelector(".field-error");
    if (error) {
      error.id = `field-error-${index + 1}`;
      error.setAttribute("role", "alert");
      control.setAttribute("aria-describedby", error.id);
    }
  }
});

document.querySelectorAll("select:not([aria-label])").forEach((control, index) => {
  control.setAttribute("aria-label", control.closest(".toolbar") ? `筛选条件 ${index + 1}` : `选择项 ${index + 1}`);
});

document.querySelectorAll(".field-error:not([role])").forEach((error) => error.setAttribute("role", "alert"));
document.querySelectorAll(".nav-item.active").forEach((item) => item.setAttribute("aria-current", "page"));
document.querySelectorAll(".segmented .segment").forEach((item) => item.setAttribute("aria-pressed", item.classList.contains("active") ? "true" : "false"));
document.querySelectorAll(".tabs").forEach((tabs) => tabs.setAttribute("role", "tablist"));
document.querySelectorAll(".tab").forEach((tab) => {
  tab.setAttribute("role", "tab");
  tab.setAttribute("aria-selected", tab.classList.contains("active") ? "true" : "false");
  tab.tabIndex = tab.classList.contains("active") ? 0 : -1;
});

if (window.lucide) {
  window.lucide.createIcons({ attrs: { "aria-hidden": "true" } });
}

const modal = document.querySelector('[role="dialog"][aria-modal="true"]');
if (modal) {
  document.querySelectorAll(".app-shell > header, .app-shell > main").forEach((region) => {
    region.inert = true;
    region.setAttribute("inert", "");
    region.setAttribute("aria-hidden", "true");
  });
  const focusable = [...modal.querySelectorAll('button, input, select, textarea, a[href]')].filter((element) => !element.disabled);
  const initialFocus = modal.querySelector("textarea, input, select") || focusable[0];
  initialFocus?.focus();
  modal.addEventListener("keydown", (event) => {
    if (event.key !== "Tab" || focusable.length === 0) return;
    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  });
}
