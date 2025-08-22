// 动态加载CSS和样式
function loadStyles() {
  // 只在直接HTML加载时加载CSS（webpack环境下会自动处理）
  if (!document.querySelector('link[href*="settings.css"]')) {
    const link = document.createElement('link');
    link.rel = 'stylesheet';
    link.href = 'settings.css';
    document.head.appendChild(link);
  }
}

let currentModels = [];

function showPage(page) {
  // 更新导航按钮状态
  document.querySelectorAll('.nav-btn').forEach(btn => btn.classList.remove('active'));
  event.target.classList.add('active');
  
  // 显示对应页面
  document.querySelectorAll('.settings-page').forEach(p => p.classList.remove('active'));
  document.getElementById(page + '-page').classList.add('active');
}

// 暴露全局函数
window.showPage = showPage;
window.closeSettings = closeSettings;
window.switchCard = switchCard;
window.addNewModel = addNewModel;
window.deleteModel = deleteModel;
window.updateModel = updateModel;

function addNewModel() {
  const modelId = 'model_' + Date.now();
  const model = {
    id: modelId,
    name: '新模型 ' + (currentModels.length + 1),
    apiUrl: '',
    apiKey: '',
    modelName: '',
    temperature: 0.7
  };
  currentModels.push(model);
  renderModels();
  saveModels();
}

function deleteModel(modelId) {
  // 如果只有一个模型配置，不允许删除
  if (currentModels.length <= 1) {
    alert('至少需要保留一个模型配置');
    return;
  }
  
  currentModels = currentModels.filter(m => m.id !== modelId);
  renderModels();
  saveModels();
}

function updateModel(modelId, field, value) {
  const model = currentModels.find(m => m.id === modelId);
  if (model) {
    model[field] = value;
    saveModels();
    updateModelCount();
  }
}

function renderModels() {
  const container = document.getElementById('models-container');
  container.innerHTML = '';
  
  currentModels.forEach(model => {
    const modelDiv = document.createElement('div');
    modelDiv.className = 'model-config';
    
    // 如果只有一个模型配置，删除按钮禁用
    const deleteButtonDisabled = currentModels.length <= 1;
    const deleteButtonStyle = deleteButtonDisabled ? 'opacity: 0.5; cursor: not-allowed;' : '';
    
    modelDiv.innerHTML = `
      <div class="model-config-header">
        <span>${model.name}</span>
        <button class="delete-model-btn" onclick="deleteModel('${model.id}')" style="${deleteButtonStyle}" ${deleteButtonDisabled ? 'disabled' : ''}>删除</button>
      </div>
      <div class="setting-item">
        <label>模型名称</label>
        <input type="text" value="${model.name}" onchange="updateModel('${model.id}', 'name', this.value)">
      </div>
      <div class="setting-item">
        <label>API URL</label>
        <input type="text" value="${model.apiUrl}" onchange="updateModel('${model.id}', 'apiUrl', this.value)" placeholder="例如: https://api.openai.com/v1/chat/completions">
      </div>
      <div class="setting-item">
        <label>API Key</label>
        <input type="password" value="${model.apiKey}" onchange="updateModel('${model.id}', 'apiKey', this.value)" placeholder="sk-...">
      </div>
      <div class="setting-item">
        <label>模型名称</label>
        <input type="text" value="${model.modelName}" onchange="updateModel('${model.id}', 'modelName', this.value)" placeholder="gpt-3.5-turbo">
      </div>
      <div class="setting-item">
        <label>温度 (Temperature)</label>
        <input type="number" min="0" max="2" step="0.1" value="${model.temperature}" onchange="updateModel('${model.id}', 'temperature', parseFloat(this.value))">
      </div>
    `;
    container.appendChild(modelDiv);
  });
  
  updateModelCount();
}

function updateModelCount() {
  const modelCount = currentModels.length;
  const cardCount = Math.max(1, modelCount); // 至少显示1张卡片
  document.getElementById('model-count').textContent = modelCount;
  document.getElementById('card-count').textContent = cardCount;
}

async function saveModels() {
  const settings = { models: currentModels };
  await window.electronAPI.saveSettings(settings);
  
  // 通知主窗口更新卡片
  if (window.electronAPI.refreshCards) {
    window.electronAPI.refreshCards();
  }
}

async function loadModels() {
  const settings = await window.electronAPI.getSettings();
  if (settings && settings.models) {
    currentModels = settings.models;
  }
  renderModels();
}

function initializeSettings() {
  const opacitySlider = document.getElementById('opacity');
  const opacityValue = document.getElementById('opacityValue');
  
  opacitySlider.addEventListener('input', (e) => {
    const value = Math.round(e.target.value * 100);
    opacityValue.textContent = value + '%';
    
    // 实时保存透明度设置
    const settings = { opacity: parseFloat(e.target.value) };
    window.electronAPI.saveSettings(settings);
  });

  // 实时保存主题设置
  document.getElementById('theme').addEventListener('change', (e) => {
    const settings = { theme: e.target.value };
    window.electronAPI.saveSettings(settings);
  });

  // 实时保存自启动设置
  document.getElementById('autoStart').addEventListener('change', (e) => {
    const settings = { autoStart: e.target.checked };
    window.electronAPI.saveSettings(settings);
  });

  // 实时保存置顶设置
  document.getElementById('alwaysOnTop').addEventListener('change', (e) => {
    const settings = { alwaysOnTop: e.target.checked };
    window.electronAPI.saveSettings(settings);
  });
}

function closeSettings() {
  window.close();
}

// 卡片切换相关变量和函数
let currentCardIndex = 0;
let totalCards = 1;

// 更新卡片信息显示
function updateCardInfo() {
  const cardInfo = document.getElementById('currentCardInfo');
  if (cardInfo) {
    cardInfo.textContent = `卡片 ${currentCardIndex + 1}/${totalCards}`;
  }
}

// 卡片切换函数
function switchCard(direction) {
  // 通过IPC触发主窗口的卡片切换
  window.electronAPI.triggerCardSwitch(direction).then(response => {
    if (!response.success) {
      console.error('卡片切换失败:', response.error);
    }
  }).catch(error => {
    console.error('卡片切换请求失败:', error);
  });
}

// 初始化页面
document.addEventListener('DOMContentLoaded', async () => {
  // 加载样式
  loadStyles();
  
  initializeSettings();
  
  // 加载当前设置
  const settings = await window.electronAPI.getSettings();
  if (settings) {
    document.getElementById('theme').value = settings.theme || 'auto';
    document.getElementById('opacity').value = settings.opacity || 1;
    document.getElementById('autoStart').checked = settings.autoStart || false;
    document.getElementById('alwaysOnTop').checked = settings.alwaysOnTop !== false;
    
    const value = Math.round((settings.opacity || 1) * 100);
    document.getElementById('opacityValue').textContent = value + '%';
  }
  
  // 加载模型配置
  await loadModels();
  
  // 监听来自主进程的卡片索引变化通知
  if (window.electronAPI && window.electronAPI.onCardIndexChange) {
    window.electronAPI.onCardIndexChange((event, cardIndex) => {
      console.log('设置页面收到卡片索引变化:', cardIndex);
      currentCardIndex = cardIndex;
      updateCardInfo();
    });
  }
  
  // 更新卡片总数和当前索引
  totalCards = currentModels.length || 1;
  if (settings && settings.currentCard !== undefined) {
    currentCardIndex = Math.max(0, Math.min(settings.currentCard, totalCards - 1));
  } else {
    currentCardIndex = 0;
  }
  updateCardInfo();
});