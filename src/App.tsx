import React, { useState, useCallback, memo, useMemo, useEffect, useRef } from 'react';
import { 
  IconButton, 
  Box, 
  Typography,
  Switch,
  Button,
  TextField,
  List,
  ListItem,
  ListItemText,
  ListItemSecondaryAction,
  Divider,
  Paper,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  LinearProgress,
  Snackbar,
  Alert
} from '@mui/material';
import { 
  Settings as SettingsIcon, 
  Home as HomeIcon,
  Mic as MicIcon,
  MicOff as MicOffIcon,
  Brightness4 as DarkModeIcon,
  Brightness7 as LightModeIcon,
  Add as AddIcon,
  Delete as DeleteIcon,
  Edit as EditIcon,
  Refresh as RefreshIcon
} from '@mui/icons-material';
import { useTheme } from './ThemeContext';
import { useApiKeys } from './ApiKeyContext';
import { getTransformersASRInstance, TransformersASRResult, ModelInfo, ModelDownloadProgress } from './transformers-asr';

const App: React.FC = () => {
  const { theme, toggleTheme } = useTheme();
  const [currentPage, setCurrentPage] = useState<'home' | 'settings'>('home');
  const [isListening, setIsListening] = useState<boolean>(false);
  const [transcript, setTranscript] = useState<string>('');
  const [isInitializing, setIsInitializing] = useState<boolean>(false);
  
  // 模型管理相关状态
  const [availableModels, setAvailableModels] = useState<ModelInfo[]>([]);
  const [currentModelId, setCurrentModelId] = useState<string>('');
  const [modelDownloadProgress, setModelDownloadProgress] = useState<Record<string, number>>({});
  
  // 错误提示状态
  const [snackbarOpen, setSnackbarOpen] = useState<boolean>(false);
  const [snackbarMessage, setSnackbarMessage] = useState<string>('');
  const [snackbarSeverity, setSnackbarSeverity] = useState<'error' | 'warning' | 'info' | 'success'>('error');
  
  const asrRef = useRef(getTransformersASRInstance());

  const handleSetCurrentPage = useCallback((page: 'home' | 'settings') => {
    setCurrentPage(page);
  }, []);

  // 显示用户提示消息的函数
  const showMessage = useCallback((message: string, severity: 'error' | 'warning' | 'info' | 'success' = 'error') => {
    setSnackbarMessage(message);
    setSnackbarSeverity(severity);
    setSnackbarOpen(true);
  }, []);

  // 刷新模型状态的核心逻辑
  const refreshModelStatusCore = useCallback(async (showSuccessMessage = true) => {
    try {
      console.log('开始刷新模型状态...');
      
      // 调用主进程的刷新方法
      const result = await window.electronAPI.speechRecognition.refreshModelStatus();
      
      if (result.success) {
        // 同时调用ASR实例的刷新方法确保状态同步
        const asr = asrRef.current;
        await asr.refreshModelStatus();
        if (showSuccessMessage) {
          showMessage('模型状态已刷新', 'success');
        }
      } else {
        throw new Error(result.error || '刷新失败');
      }
    } catch (error) {
      // 检查是否是在设置页面被拒绝
      if ((error as Error).message.includes('在设置页面不允许手动刷新模型状态')) {
        // 在设置页面时，只调用ASR实例的刷新方法
        try {
          const asr = asrRef.current;
          await asr.refreshModelStatus();
          if (showSuccessMessage) {
            showMessage('模型状态已刷新', 'success');
          }
        } catch (asrError) {
          console.error('ASR刷新模型状态失败:', asrError);
          showMessage('刷新模型状态失败: ' + (asrError as Error).message, 'error');
        }
      } else {
        console.error('刷新模型状态失败:', error);
        showMessage('刷新模型状态失败: ' + (error as Error).message, 'error');
      }
    }
  }, [showMessage]);

  // 刷新模型状态（带成功提示）
  const handleRefreshModels = useCallback(async () => {
    await refreshModelStatusCore(true);
  }, [refreshModelStatusCore]);

  // 添加 IPC 消息监听
  useEffect(() => {
    // 监听导航到设置页面的消息
    const navigateToSettings = () => {
      setCurrentPage('settings');
    };
    
    // 监听触发主页对话按钮的消息
    const triggerHomeDialog = () => {
      setIsListening(prev => !prev);
    };

    // 监听下载进度更新
    const handleDownloadProgress = (data: { progress: number; loaded: number; total: number }) => {
      console.log(`下载进度: ${data.progress.toFixed(1)}% (${(data.loaded / 1024 / 1024).toFixed(2)} MB / ${(data.total / 1024 / 1024).toFixed(2)} MB)`);
      // 更新 Whisper Tiny ONNX 模型的下载进度
      if (asrRef.current) {
        asrRef.current.updateWhisperTinyONNXProgress(data.progress);
      }
    };

    // 防抖定时器
    let modelFilesChangedTimeout: NodeJS.Timeout | null = null;
    
    // 监听模型文件变化
    const handleModelFilesChanged = async () => {
      console.log('检测到模型文件变化，更新状态...');
      
      // 清除之前的定时器
      if (modelFilesChangedTimeout) {
        clearTimeout(modelFilesChangedTimeout);
      }
      
      // 设置防抖延迟
      modelFilesChangedTimeout = setTimeout(async () => {
        try {
          // 只调用ASR实例的刷新方法，避免触发主进程的刷新
          const asr = asrRef.current;
          if (asr) {
            await asr.refreshModelStatus();
          }
        } catch (error) {
          console.error('更新模型状态失败:', error);
        }
      }, 1000); // 1秒防抖延迟
    };

    window.electronAPI.ipcRenderer.on('navigate-to-settings', navigateToSettings);
    window.electronAPI.ipcRenderer.on('trigger-home-dialog', triggerHomeDialog);
    window.electronAPI.ipcRenderer.on('download-progress', handleDownloadProgress);
    window.electronAPI.ipcRenderer.on('model-files-changed', handleModelFilesChanged);

    // 清理监听器
    return () => {
      window.electronAPI.ipcRenderer.removeListener('navigate-to-settings', navigateToSettings);
      window.electronAPI.ipcRenderer.removeListener('trigger-home-dialog', triggerHomeDialog);
      window.electronAPI.ipcRenderer.removeListener('download-progress', handleDownloadProgress);
      window.electronAPI.ipcRenderer.removeListener('model-files-changed', handleModelFilesChanged);
    };
  }, []);

  // 关闭提示消息
  const handleSnackbarClose = useCallback(() => {
    setSnackbarOpen(false);
  }, []);

  const handleSettingsClick = useCallback(() => {
    handleSetCurrentPage('settings');
  }, [handleSetCurrentPage]);

  const toggleListening = useCallback(async () => {
    try {
      if (isListening) {
        // 停止监听
        await asrRef.current.stopListening();
        setIsListening(false);
        console.log('语音识别已停止');
      } else {
        // 开始监听
        setIsInitializing(true);
        
        if (!asrRef.current.initialized) {
          console.log('正在初始化ASR模型...');
          await asrRef.current.initialize();
        }
        
        await asrRef.current.startListening();
        setIsListening(true);
        setIsInitializing(false);
        console.log('语音识别已开始');
      }
    } catch (error) {
      console.error('语音识别操作失败:', error);
      setIsListening(false);
      setIsInitializing(false);
      
      // 提供更友好的错误提示
      let errorMessage = '语音识别失败';
      if (error instanceof Error) {
        if (error.message.includes('internet connection') || error.message.includes('network')) {
          errorMessage = '网络连接问题，请检查网络连接后重试';
        } else if (error.message.includes('model')) {
          errorMessage = '未检测到本地模型，请在设置页面下载语音识别模型';
        } else if (error.message.includes('microphone') || error.message.includes('getUserMedia')) {
          errorMessage = '无法访问麦克风，请检查麦克风权限';
        } else {
          errorMessage = `语音识别错误: ${error.message}`;
        }
      }
      
      // 显示用户友好的错误提示
      showMessage(errorMessage, 'error');
    }
  }, [isListening, showMessage]);

  // 设置ASR事件监听器
  useEffect(() => {
    const asr = asrRef.current;
    
    const handleResult = (result: TransformersASRResult) => {
      setTranscript(prev => prev + (prev ? ' ' : '') + result.text);
      console.log('识别结果:', result.text);
    };
    
    const handleError = (error: Error) => {
      console.error('ASR错误:', error);
      setIsListening(false);
      setIsInitializing(false);
      
      // 显示用户友好的错误提示
      let errorMessage = 'ASR错误';
      if (error.message.includes('model')) {
        errorMessage = '未检测到本地模型，请在设置页面下载语音识别模型';
      } else {
        errorMessage = `语音识别错误: ${error.message}`;
      }
      showMessage(errorMessage, 'error');
    };
    
    const handleModelProgress = (progress: { status: string; progress?: number }) => {
      console.log('模型下载进度:', progress);
    };

    // 模型管理事件监听器
    const handleModelListUpdated = (models: ModelInfo[]) => {
      setAvailableModels(models);
    };

    const handleModelDownloadProgress = (progress: ModelDownloadProgress) => {
      setModelDownloadProgress(prev => ({
        ...prev,
        [progress.modelId]: progress.progress
      }));
    };
    
    asr.on('result', handleResult);
    asr.on('error', handleError);
    asr.on('modelProgress', handleModelProgress);
    asr.on('modelListUpdated', handleModelListUpdated);
    asr.on('modelDownloadProgress', handleModelDownloadProgress);
    
    // 初始化模型列表
    setAvailableModels(asr.getAvailableModels());
    setCurrentModelId(asr.getCurrentModelId());
    
    return () => {
      asr.off('result', handleResult);
      asr.off('error', handleError);
      asr.off('modelProgress', handleModelProgress);
      asr.off('modelListUpdated', handleModelListUpdated);
      asr.off('modelDownloadProgress', handleModelDownloadProgress);
    };
  }, [showMessage]);

  // 使用useMemo优化组件渲染
  const SettingsPage = useMemo(() => memo(() => {
    const [openDialog, setOpenDialog] = useState(false);
    const [editDialogOpen, setEditDialogOpen] = useState(false);
    const [newKeyName, setNewKeyName] = useState('');
    const [newKeyValue, setNewKeyValue] = useState('');
    const [newKeyBaseUrl, setNewKeyBaseUrl] = useState('');
    const [editingKeyId, setEditingKeyId] = useState<string | null>(null);
    const [micTestResult, setMicTestResult] = useState<{ success: boolean; error?: string; audioLevel?: number } | null>(null);
    const [isTesting, setIsTesting] = useState(false);
    const [currentAudioLevel, setCurrentAudioLevel] = useState(0);
    const [currentDecibels, setCurrentDecibels] = useState(-60);
    const { apiKeys, addApiKey, removeApiKey, updateApiKey } = useApiKeys();
    
    // 滚动位置管理
    const scrollContainerRef = useRef<HTMLDivElement>(null);
    const [savedScrollTop, setSavedScrollTop] = useState(0);

    const handleAddKey = () => {
      if (newKeyName.trim() && newKeyValue.trim()) {
        addApiKey({
          name: newKeyName,
          key: newKeyValue,
          baseUrl: newKeyBaseUrl || undefined
        });
        setNewKeyName('');
        setNewKeyValue('');
        setNewKeyBaseUrl('');
        setOpenDialog(false);
      }
    };

    const handleEditKey = (id: string) => {
      const apiKey = apiKeys.find(key => key.id === id);
      if (apiKey) {
        setNewKeyName(apiKey.name);
        setNewKeyValue(apiKey.key);
        setNewKeyBaseUrl(apiKey.baseUrl || '');
        setEditingKeyId(id);
        setEditDialogOpen(true);
      }
    };

    const handleUpdateKey = () => {
      if (editingKeyId && newKeyName.trim() && newKeyValue.trim()) {
        updateApiKey(editingKeyId, {
          name: newKeyName,
          key: newKeyValue,
          baseUrl: newKeyBaseUrl || undefined
        });
        setNewKeyName('');
        setNewKeyValue('');
        setNewKeyBaseUrl('');
        setEditingKeyId(null);
        setEditDialogOpen(false);
      }
    };

    // 模型管理处理函数
    const handleDownloadModel = async (modelId: string) => {
      try {
        // 根据模型ID调用对应的下载方法
        switch (modelId) {
          case 'Xenova/whisper-tiny':
            await asrRef.current.downloadWhisperTinyONNX();
            break;
          case 'Xenova/whisper-tiny.en':
            await asrRef.current.downloadWhisperTinyENONNX();
            break;
          case 'Xenova/whisper-base':
            await asrRef.current.downloadWhisperBaseONNX();
            break;
          case 'Xenova/whisper-base.en':
            await asrRef.current.downloadWhisperBaseENONNX();
            break;
          default:
            throw new Error(`不支持的模型: ${modelId}`);
        }
        showMessage(`模型 ${modelId} 下载完成`, 'success');
      } catch (error) {
        console.error(`模型 ${modelId} 下载失败:`, error);
        showMessage(`模型 ${modelId} 下载失败: ${(error as Error).message}`, 'error');
      }
    };

    // 切换模型
    const handleSwitchModel = async (modelId: string) => {
      try {
        console.log('开始切换模型:', modelId);
        const modelInfo = availableModels.find(m => m.id === modelId);
        if (!modelInfo) {
          throw new Error('未找到指定模型');
        }

        // 检查是否正在使用语音识别
        if (isListening) {
          showMessage('请先停止语音识别，然后再切换模型', 'warning');
          return;
        }

        console.log('调用ASR实例的switchToModel方法');
        await asrRef.current.switchToModel(modelId);
        console.log('ASR模型切换成功，更新当前模型ID');
        setCurrentModelId(modelId);
        showMessage(`已切换到模型: ${modelInfo.name}`, 'success');
      } catch (error) {
        console.error(`切换模型失败:`, error);
        showMessage(`切换模型失败: ${(error as Error).message}`, 'error');
      }
    };

    // 处理模型卡片点击事件
    const handleModelCardClick = (modelId: string) => {
      console.log('模型卡片被点击:', modelId);
      const modelInfo = availableModels.find(m => m.id === modelId);
      if (modelInfo) {
        console.log('模型信息:', modelInfo);
        console.log('当前模型ID:', currentModelId);
        // 只允许切换已下载的模型，不自动触发下载
        if (modelInfo.downloaded && currentModelId !== modelId) {
          console.log('准备切换模型:', modelId);
          handleSwitchModel(modelId);
        } else if (modelInfo.downloaded && currentModelId === modelId) {
          console.log('模型已经是当前使用的模型');
          showMessage('该模型已经是当前使用的模型', 'info');
        } else {
          console.log('模型未下载，需要先下载');
          showMessage('请先下载模型文件，然后才能使用', 'warning');
        }
        // 如果模型未下载，不执行任何操作，用户需要点击下载按钮
      } else {
        console.log('未找到模型信息:', modelId);
        showMessage('未找到模型信息', 'error');
      }
    };

    const handleDeleteModel = async (modelId: string) => {
      try {
        // 暂时显示功能未实现的提示
        showMessage('模型删除功能正在开发中，请手动删除模型文件', 'info');
        
        // TODO: 实现模型删除功能
        // 可以通过以下方式实现：
        // 1. 在 electron-api.d.ts 中添加 deleteModelFiles 方法
        // 2. 在主进程中实现删除模型文件的逻辑
        // 3. 通过 IPC 调用删除功能
        
        console.log(`请求删除模型: ${modelId}`);
      } catch (error) {
        console.error(`删除模型失败:`, error);
        showMessage(`删除模型失败: ${(error as Error).message}`, 'error');
      }
    };

    const handleMicrophoneTest = async () => {
      if (isTesting) {
        // 停止测试
        try {
          await asrRef.current.stopMicrophoneTest();
          setIsTesting(false);
          setMicTestResult({ success: true, audioLevel: currentAudioLevel });
        } catch (error) {
          setMicTestResult({
            success: false,
            error: (error as Error).message
          });
          setIsTesting(false);
        }
      } else {
        // 开始测试
        setIsTesting(true);
        setMicTestResult(null);
        setCurrentAudioLevel(0);
        setCurrentDecibels(-60);
        
        try {
          const result = await asrRef.current.startMicrophoneTest();
          if (!result.success) {
            setMicTestResult({
              success: false,
              error: result.error
            });
            setIsTesting(false);
          }
        } catch (error) {
          setMicTestResult({
            success: false,
            error: (error as Error).message
          });
          setIsTesting(false);
        }
      }
    };

    // 监听麦克风测试级别
    useEffect(() => {
      const asr = asrRef.current;
      
      const handleMicTestLevel = (data: { level: number; decibels: number }) => {
        setCurrentAudioLevel(data.level);
        setCurrentDecibels(data.decibels);
      };

      const handleMicTestStopped = () => {
        setIsTesting(false);
      };

      asr.on('micTestLevel', handleMicTestLevel);
      asr.on('micTestStopped', handleMicTestStopped);

      return () => {
        asr.off('micTestLevel', handleMicTestLevel);
        asr.off('micTestStopped', handleMicTestStopped);
      };
    }, []);

    // 滚动位置管理 - 保存和恢复滚动位置
    useEffect(() => {
      const container = scrollContainerRef.current;
      if (!container) return;

      // 保存当前滚动位置
      const currentScrollTop = container.scrollTop;
      if (currentScrollTop > 0) {
        setSavedScrollTop(currentScrollTop);
      }
    }, [apiKeys, availableModels, currentModelId, isTesting, micTestResult]);

    // 恢复滚动位置 - 使用单独的useEffect避免循环依赖
    useEffect(() => {
      const container = scrollContainerRef.current;
      if (container && savedScrollTop > 0) {
        // 使用 requestAnimationFrame 确保在DOM更新后执行
        requestAnimationFrame(() => {
          if (container) {
            container.scrollTop = savedScrollTop;
          }
        });
      }
    }, [savedScrollTop]);

    return (
      <Box 
        ref={scrollContainerRef}
        sx={{ 
          height: '100vh', 
          width: '100vw',
          display: 'flex', 
          flexDirection: 'column',
          position: 'relative',
          overflowX: 'hidden', // 禁止横向滚动
          overflowY: 'auto'   // 允许纵向滚动
        }}
      >
        {/* 主页按钮 - 左上角 */}
        <IconButton
          onClick={() => handleSetCurrentPage('home')}
          sx={{
            position: 'absolute',
            top: 16,
            left: 16,
            zIndex: 1000,
            width: 60, 
            height: 60,
          }}
        >
          <HomeIcon sx={{ fontSize: 36 }} />
        </IconButton>
        
        {/* 主题切换开关 - 右上角 */}
        <Box
          sx={{
            position: 'absolute',
            top: 16,
            right: 16,
            zIndex: 1000,
            display: 'flex',
            alignItems: 'center',
            backgroundColor: 'rgba(0, 0, 0, 0.05)',
            borderRadius: 4,
            padding: '4px 8px'
          }}
        >
          <LightModeIcon sx={{ fontSize: 20, mr: 1, color: theme === 'light' ? 'primary.main' : 'text.secondary' }} />
          <Switch
            checked={theme === 'dark'}
            onChange={toggleTheme}
            size="small"
          />
          <DarkModeIcon sx={{ fontSize: 20, ml: 1, color: theme === 'dark' ? 'primary.main' : 'text.secondary' }} />
        </Box>
        
        <Box 
          sx={{ 
            display: 'flex', 
            flexDirection: 'column',
            alignItems: 'center',
            justifyContent: 'flex-start',
            flexGrow: 1,
            pt: 8, // 为顶部按钮留出空间
            px: 2,
            width: '100%', // 确保内容区域占满宽度
            boxSizing: 'border-box' // 包含padding在宽度计算内
          }}
        >
          <Typography variant="h4" sx={{ mb: 3 }}>设置页面</Typography>
          
          <Paper sx={{ width: '100%', maxWidth: 600, p: 3, mb: 3, boxSizing: 'border-box' }}>
            <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2, flexWrap: 'wrap', gap: 1 }}>
              <Typography variant="h6">API 管理</Typography>
              <Button 
                variant="contained" 
                startIcon={<AddIcon />} 
                onClick={() => setOpenDialog(true)}
              >
                添加Key
              </Button>
            </Box>
            
            {apiKeys.length === 0 ? (
              <Typography sx={{ textAlign: 'center', py: 2 }}>暂无 API Key，请添加一个</Typography>
            ) : (
              <List>
                {apiKeys.map((apiKey) => (
                  <React.Fragment key={apiKey.id}>
                    <ListItem>
                      <ListItemText 
                        primary={apiKey.name} 
                        secondary={apiKey.baseUrl ? `Base URL: ${apiKey.baseUrl}` : '默认 OpenAI API'} 
                      />
                      <ListItemSecondaryAction>
                        <IconButton 
                          edge="end" 
                          aria-label="edit"
                          onClick={() => handleEditKey(apiKey.id)}
                          sx={{ mr: 1 }}
                        >
                          <EditIcon />
                        </IconButton>
                        <IconButton 
                          edge="end" 
                          aria-label="delete" 
                          onClick={() => removeApiKey(apiKey.id)}
                        >
                          <DeleteIcon />
                        </IconButton>
                      </ListItemSecondaryAction>
                    </ListItem>
                    <Divider />
                  </React.Fragment>
                ))}
              </List>
            )}
          </Paper>
          
          {/* 麦克风测试部分 */}
          <Paper sx={{ width: '100%', maxWidth: 600, p: 3, mb: 3, boxSizing: 'border-box' }}>
            <Typography variant="h6" sx={{ mb: 2 }}>麦克风测试</Typography>
            
            <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
              <Button 
                variant="contained" 
                color={isTesting ? "error" : "secondary"}
                onClick={handleMicrophoneTest}
                startIcon={isTesting ? <Typography>⏹️</Typography> : <Typography>🎤</Typography>}
              >
                {isTesting ? '停止测试' : '开始测试麦克风'}
              </Button>
              
              {/* 分贝进度条 */}
              {isTesting && (
                <Box sx={{ width: '100%' }}>
                  <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 1 }}>
                    <Typography variant="body2" color="text.secondary">
                      音量级别
                    </Typography>
                    <Typography variant="body2" color="text.secondary">
                      {currentDecibels.toFixed(1)} dB
                    </Typography>
                  </Box>
                  <LinearProgress 
                    variant="determinate" 
                    value={currentAudioLevel} 
                    sx={{ 
                      height: 10, 
                      borderRadius: 5,
                      backgroundColor: theme === 'dark' ? 'grey.700' : 'grey.300',
                      '& .MuiLinearProgress-bar': {
                        borderRadius: 5,
                        backgroundColor: currentAudioLevel > 80 ? '#f44336' : 
                                       currentAudioLevel > 50 ? '#ff9800' : 
                                       currentAudioLevel > 20 ? '#4caf50' : '#2196f3'
                      }
                    }}
                  />
                  <Box sx={{ display: 'flex', justifyContent: 'space-between', mt: 0.5 }}>
                    <Typography variant="caption" color="text.secondary">静音</Typography>
                    <Typography variant="caption" color="text.secondary">适中</Typography>
                    <Typography variant="caption" color="text.secondary">过大</Typography>
                  </Box>
                </Box>
              )}
              
              {micTestResult && !isTesting && (
                <Paper 
                  variant="outlined" 
                  sx={{ 
                    p: 2, 
                    backgroundColor: micTestResult.success ? 'success.light' : 'error.light',
                    color: micTestResult.success ? 'success.contrastText' : 'error.contrastText'
                  }}
                >
                  {micTestResult.success ? (
                    <Box>
                      <Typography variant="body1" sx={{ fontWeight: 'bold' }}>
                        ✅ 麦克风测试完成！
                      </Typography>
                      <Typography variant="body2" sx={{ mt: 1 }}>
                        最大音频级别: {micTestResult.audioLevel?.toFixed(1) || 'N/A'}%
                      </Typography>
                      <Typography variant="caption" sx={{ mt: 1, display: 'block' }}>
                        您的麦克风工作正常，可以进行语音识别。
                      </Typography>
                    </Box>
                  ) : (
                    <Box>
                      <Typography variant="body1" sx={{ fontWeight: 'bold' }}>
                        ❌ 麦克风测试失败
                      </Typography>
                      <Typography variant="body2" sx={{ mt: 1 }}>
                        错误: {micTestResult.error}
                      </Typography>
                      <Typography variant="caption" sx={{ mt: 1, display: 'block' }}>
                        请检查麦克风权限或设备连接。
                      </Typography>
                    </Box>
                  )}
                </Paper>
              )}
              
              <Typography variant="caption" color="text.secondary">
                {isTesting 
                  ? '麦克风正在测试中，请对着麦克风说话或制造声音。完成后点击"停止测试"按钮。'
                  : '点击"开始测试麦克风"按钮检查您的麦克风是否正常工作。测试将实时显示音频级别。'
                }
              </Typography>
            </Box>
          </Paper>
          
          {/* ASR 模型管理部分 */}
          <Paper sx={{ width: '100%', maxWidth: 600, p: 3, mb: 3, boxSizing: 'border-box' }}>
            <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
              <Typography variant="h6">语音识别模型管理</Typography>
              <IconButton 
                onClick={handleRefreshModels}
                size="small"
                title="刷新模型状态"
                sx={{ ml: 1 }}
              >
                <RefreshIcon />
              </IconButton>
            </Box>
            
            <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
              <Typography variant="body2" color="text.secondary">
                选择和管理用于语音识别的模型。所有模型都经过优化，可在CPU上高效运行。
              </Typography>
              
              {/* 当前使用模型状态显示 */}
              {currentModelId && (
                <Paper 
                  sx={{ 
                    p: 2, 
                    backgroundColor: theme === 'dark' ? 'rgba(76, 175, 80, 0.1)' : 'rgba(76, 175, 80, 0.05)',
                    border: theme === 'dark' ? '1px solid rgba(76, 175, 80, 0.3)' : '1px solid rgba(76, 175, 80, 0.2)'
                  }}
                >
                  <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                    <Typography variant="body2" sx={{ fontWeight: 'bold' }}>
                      当前使用模型:
                    </Typography>
                    <Typography variant="body2" color="success.main" sx={{ fontWeight: 'bold' }}>
                      {availableModels.find(m => m.id === currentModelId)?.name || currentModelId}
                    </Typography>
                  </Box>
                </Paper>
              )}
              
              {availableModels.map((model) => (
                <Paper 
                  key={model.id}
                  variant="outlined" 
                  sx={{ 
                    p: 2,
                    backgroundColor: currentModelId === model.id ? 
                      (theme === 'dark' ? 'rgba(76, 175, 80, 0.1)' : 'rgba(76, 175, 80, 0.05)') : 
                      (model.downloaded && currentModelId !== model.id ? 
                        (theme === 'dark' ? 'rgba(33, 150, 243, 0.1)' : 'rgba(33, 150, 243, 0.05)') : 
                        'transparent'),
                    border: currentModelId === model.id ? 
                      (theme === 'dark' ? '2px solid rgba(76, 175, 80, 0.5)' : '2px solid rgba(76, 175, 80, 0.3)') : 
                      (model.downloaded && currentModelId !== model.id ? 
                        (theme === 'dark' ? '2px solid rgba(33, 150, 243, 0.5)' : '2px solid rgba(33, 150, 243, 0.3)') : 
                        '1px solid rgba(0, 0, 0, 0.12)'),
                    transition: 'all 0.2s ease-in-out',
                    cursor: model.downloaded && currentModelId !== model.id ? 'pointer' : 'default',
                    '&:hover': {
                      backgroundColor: model.downloaded && currentModelId !== model.id ? 
                        (theme === 'dark' ? 'rgba(33, 150, 243, 0.2)' : 'rgba(33, 150, 243, 0.1)') : 
                        'transparent'
                    }
                  }}
                  onClick={() => handleModelCardClick(model.id)}
                  title={
                    model.downloaded 
                      ? (currentModelId === model.id 
                          ? '当前正在使用此模型' 
                          : '点击切换到此模型')
                      : '点击下载按钮下载模型'
                  }
                >
                  <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
                    <Box sx={{ flex: 1 }}>
                      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 1 }}>
                        <Typography variant="subtitle1" sx={{ fontWeight: 'bold' }}>
                          {model.name}
                        </Typography>
                        {currentModelId === model.id && (
                          <Typography 
                            variant="caption" 
                            sx={{ 
                              backgroundColor: 'success.main',
                              color: 'success.contrastText',
                              px: 1,
                              py: 0.25,
                              borderRadius: 1,
                              fontSize: '0.7rem',
                              fontWeight: 'bold'
                            }}
                          >
                            当前使用
                          </Typography>
                        )}
                        {model.downloaded && currentModelId !== model.id && (
                          <Typography 
                            variant="caption" 
                            sx={{ 
                              backgroundColor: 'primary.main',
                              color: 'primary.contrastText',
                              px: 1,
                              py: 0.25,
                              borderRadius: 1,
                              fontSize: '0.7rem',
                              fontWeight: 'bold'
                            }}
                          >
                            可切换
                          </Typography>
                        )}
                        {!model.downloaded && (
                          <Typography 
                            variant="caption" 
                            sx={{ 
                              backgroundColor: 'warning.main',
                              color: 'warning.contrastText',
                              px: 1,
                              py: 0.25,
                              borderRadius: 1,
                              fontSize: '0.7rem',
                              fontWeight: 'bold'
                            }}
                          >
                            未下载
                          </Typography>
                        )}
                      </Box>
                      
                      <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
                        {model.description}
                      </Typography>
                      
                      <Box sx={{ display: 'flex', gap: 2, mb: 1 }}>
                        <Typography variant="caption">
                          <strong>大小:</strong> {model.size}
                        </Typography>
                        <Typography variant="caption">
                          <strong>语言:</strong> {model.languages.join(', ')}
                        </Typography>
                      </Box>
                      
                      {/* 下载进度 */}
                      {model.downloading && (
                        <Box sx={{ mt: 1 }}>
                          <Typography variant="caption" color="text.secondary">
                            下载中: {model.downloadProgress.toFixed(1)}%
                          </Typography>
                          <LinearProgress 
                            variant="determinate" 
                            value={model.downloadProgress} 
                            sx={{ mt: 0.5, height: 6, borderRadius: 3 }}
                          />
                        </Box>
                      )}
                    </Box>
                    
                    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1, ml: 2 }}>
                      {!model.downloaded && !model.downloading && (
                        <Button 
                          variant="contained" 
                          size="small"
                          onClick={(e) => {
                            e.stopPropagation();
                            handleDownloadModel(model.id);
                          }}
                          sx={{ minWidth: 80 }}
                        >
                          下载
                        </Button>
                      )}
                      
                      {model.downloading && (
                        <Button 
                          variant="outlined" 
                          size="small"
                          disabled
                          sx={{ minWidth: 80 }}
                        >
                          下载中...
                        </Button>
                      )}
                      
                      {model.downloaded && !model.downloading && (
                        <>
                          {currentModelId !== model.id && (
                            <Button 
                              variant="contained" 
                              color="primary"
                              size="small"
                              onClick={(e) => {
                                e.stopPropagation();
                                handleSwitchModel(model.id);
                              }}
                              sx={{ minWidth: 80 }}
                            >
                              切换使用
                            </Button>
                          )}
                          
                          {currentModelId === model.id && (
                            <Button 
                              variant="outlined" 
                              color="success"
                              size="small"
                              disabled
                              sx={{ minWidth: 80 }}
                            >
                              使用中
                            </Button>
                          )}
                          
                          <Button 
                            variant="outlined" 
                            color="error"
                            size="small"
                            onClick={(e) => {
                              e.stopPropagation();
                              handleDeleteModel(model.id);
                            }}
                            disabled={currentModelId === model.id}
                            sx={{ minWidth: 80 }}
                          >
                            删除
                          </Button>
                        </>
                      )}
                    </Box>
                  </Box>
                </Paper>
              ))}
              
              {/* 模型使用提示 */}
              {availableModels.every(model => !model.downloaded) && (
                <Paper 
                  sx={{ 
                    p: 2, 
                    backgroundColor: theme === 'dark' ? 'warning.dark' : 'warning.light',
                    color: theme === 'dark' ? 'warning.contrastText' : 'warning.contrastText',
                    border: `1px solid ${theme === 'dark' ? 'warning.main' : 'warning.main'}`
                  }}
                >
                  <Typography variant="body2" sx={{ fontWeight: 'bold', mb: 1 }}>
                    ⚠️ 提示：尚未下载任何模型
                  </Typography>
                  <Typography variant="body2">
                    请先下载至少一个语音识别模型才能使用语音识别功能。建议下载 "Whisper Tiny (多语言)" 模型开始使用。
                  </Typography>
                </Paper>
              )}
              
              <Typography variant="caption" color="text.secondary" sx={{ mt: 1 }}>
                💡 建议：
                <br />• 中英混合识别：选择 "Whisper Tiny (多语言)" 或 "Whisper Base (多语言)"
                <br />• 纯英文识别：选择对应的 English 版本以获得更高准确度
                <br />• 设备性能较低：优先选择 Tiny 版本
                <br />• 追求准确度：选择 Base 版本
              </Typography>
            </Box>
          </Paper>
        </Box>
        
        {/* 添加 API Key对话框 */}
        <Dialog open={openDialog} onClose={() => setOpenDialog(false)}>
          <DialogTitle>添加新的 API Key</DialogTitle>
          <DialogContent>
            <TextField
              autoFocus
              margin="dense"
              label="渠道名称"
              type="text"
              fullWidth
              variant="standard"
              value={newKeyName}
              onChange={(e) => setNewKeyName(e.target.value)}
              sx={{ mb: 2 }}
            />
            <TextField
              margin="dense"
              label="API Key"
              type="password"
              fullWidth
              variant="standard"
              value={newKeyValue}
              onChange={(e) => setNewKeyValue(e.target.value)}
              sx={{ mb: 2 }}
            />
            <TextField
              margin="dense"
              label="Base URL (可选)"
              type="text"
              fullWidth
              variant="standard"
              value={newKeyBaseUrl}
              onChange={(e) => setNewKeyBaseUrl(e.target.value)}
              helperText="对于兼容 OpenAI 格式的第三方 API，如本地部署的模型"
            />
          </DialogContent>
          <DialogActions>
            <Button onClick={() => setOpenDialog(false)}>取消</Button>
            <Button onClick={handleAddKey} variant="contained">添加</Button>
          </DialogActions>
        </Dialog>

        {/* 编辑 API Key对话框 */}
        <Dialog open={editDialogOpen} onClose={() => setEditDialogOpen(false)}>
          <DialogTitle>编辑 API Key</DialogTitle>
          <DialogContent>
            <TextField
              autoFocus
              margin="dense"
              label="渠道名称"
              type="text"
              fullWidth
              variant="standard"
              value={newKeyName}
              onChange={(e) => setNewKeyName(e.target.value)}
              sx={{ mb: 2 }}
            />
            <TextField
              margin="dense"
              label="API Key"
              type="password"
              fullWidth
              variant="standard"
              value={newKeyValue}
              onChange={(e) => setNewKeyValue(e.target.value)}
              sx={{ mb: 2 }}
            />
            <TextField
              margin="dense"
              label="Base URL (可选)"
              type="text"
              fullWidth
              variant="standard"
              value={newKeyBaseUrl}
              onChange={(e) => setNewKeyBaseUrl(e.target.value)}
              helperText="对于兼容 OpenAI 格式的第三方 API，如本地部署的模型"
            />
          </DialogContent>
          <DialogActions>
            <Button onClick={() => setEditDialogOpen(false)}>取消</Button>
            <Button onClick={handleUpdateKey} variant="contained">更新</Button>
          </DialogActions>
        </Dialog>

        
      </Box>
    );
  }), [handleSetCurrentPage, theme, toggleTheme, availableModels, currentModelId, showMessage]);

  const HomePage = useMemo(() => memo(() => (
    <Box 
      sx={{ 
        height: '100vh', 
        width: '100vw',
        display: 'flex', 
        flexDirection: 'column',
        position: 'relative',
        alignItems: 'center',
        justifyContent: 'center'
      }}
    >
      {/* 语音按钮 */}
      <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 3 }}>
        <button 
          className={`voice-button ${isListening ? 'listening' : ''} ${isInitializing ? 'initializing' : ''}`}
          onClick={toggleListening}
          disabled={isInitializing}
        >
          {isInitializing ? (
            <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
              <Typography sx={{ fontSize: 14, color: 'white' }}>初始化中...</Typography>
            </Box>
          ) : isListening ? (
            <MicOffIcon sx={{ fontSize: 50, color: 'white' }} />
          ) : (
            <MicIcon sx={{ fontSize: 50, color: 'white' }} />
          )}
        </button>
        
        {/* 状态指示 */}
        <Typography variant="h6" color="text.secondary">
          {isInitializing ? '正在初始化语音识别...' : isListening ? '正在监听...' : '点击开始语音识别'}
        </Typography>
        
        {/* 转录结果显示 */}
        {transcript && (
          <Paper 
            elevation={3}
            sx={{ 
              p: 2, 
              maxWidth: '80%', 
              minHeight: 100,
              backgroundColor: theme === 'dark' ? 'grey.800' : 'grey.100',
              borderRadius: 2
            }}
          >
            <Typography variant="h6" gutterBottom>识别结果:</Typography>
            <Typography sx={{ whiteSpace: 'pre-wrap', wordBreak: 'break-word' }}>
              {transcript}
            </Typography>
            <Button 
              size="small" 
              onClick={() => setTranscript('')}
              sx={{ mt: 1 }}
            >
              清除
            </Button>
          </Paper>
        )}
      </Box>
      
      {/* 设置按钮 - 左上角 */}
      <IconButton
        onClick={handleSettingsClick}
        sx={{
          position: 'absolute',
          top: 16,
          left: 16,
          zIndex: 1000,
          width: 60, 
          height: 60,
        }}
      >
        <SettingsIcon sx={{ fontSize: 36 }} />
      </IconButton>
      
      {/* 主题切换开关 - 右上角 */}
      <Box
        sx={{
          position: 'absolute',
          top: 16,
          right: 16,
          zIndex: 1000,
          display: 'flex',
          alignItems: 'center',
          backgroundColor: 'rgba(0, 0, 0, 0.05)',
          borderRadius: 4,
          padding: '4px 8px'
        }}
      >
        <LightModeIcon sx={{ fontSize: 20, mr: 1, color: theme === 'light' ? 'primary.main' : 'text.secondary' }} />
        <Switch
          checked={theme === 'dark'}
          onChange={toggleTheme}
          size="small"
        />
        <DarkModeIcon sx={{ fontSize: 20, ml: 1, color: theme === 'dark' ? 'primary.main' : 'text.secondary' }} />
      </Box>
    </Box>
  )), [handleSettingsClick, isListening, isInitializing, toggleListening, theme, toggleTheme, transcript]);

  if (currentPage === 'settings') {
    return (
      <>
        <SettingsPage />
        <Snackbar
          open={snackbarOpen}
          autoHideDuration={6000}
          onClose={handleSnackbarClose}
          anchorOrigin={{ vertical: 'top', horizontal: 'center' }}
        >
          <Alert 
            onClose={handleSnackbarClose} 
            severity={snackbarSeverity}
            sx={{ width: '100%' }}
          >
            {snackbarMessage}
          </Alert>
        </Snackbar>
      </>
    );
  }

  return (
    <>
      <HomePage />
      <Snackbar
        open={snackbarOpen}
        autoHideDuration={6000}
        onClose={handleSnackbarClose}
        anchorOrigin={{ vertical: 'top', horizontal: 'center' }}
      >
        <Alert 
          onClose={handleSnackbarClose} 
          severity={snackbarSeverity}
          sx={{ width: '100%' }}
        >
          {snackbarMessage}
        </Alert>
      </Snackbar>
    </>
  );
};

export default memo(App);