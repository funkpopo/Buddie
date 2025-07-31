import React, { useState, useCallback, memo, useMemo, useEffect } from 'react';
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
  DialogActions
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
  Edit as EditIcon
} from '@mui/icons-material';
import { useTheme } from './ThemeContext';
import { useApiKeys } from './ApiKeyContext';

const App: React.FC = () => {
  const { theme, toggleTheme } = useTheme();
  const [currentPage, setCurrentPage] = useState<'home' | 'settings'>('home');
  const [isListening, setIsListening] = useState<boolean>(false);

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

    window.electronAPI.ipcRenderer.on('navigate-to-settings', navigateToSettings);
    window.electronAPI.ipcRenderer.on('trigger-home-dialog', triggerHomeDialog);

    // 清理监听器
    return () => {
      window.electronAPI.ipcRenderer.removeListener('navigate-to-settings', navigateToSettings);
      window.electronAPI.ipcRenderer.removeListener('trigger-home-dialog', triggerHomeDialog);
    };
  }, []);

  const handleSetCurrentPage = useCallback((page: 'home' | 'settings') => {
    setCurrentPage(page);
  }, []);

  const handleSettingsClick = useCallback(() => {
    handleSetCurrentPage('settings');
  }, [handleSetCurrentPage]);

  const toggleListening = useCallback(() => {
    setIsListening(prev => {
      const newState = !prev;
      console.log(`语音聆听状态: ${newState ? '开启' : '关闭'}`);
      // 这里可以添加实际的语音处理逻辑
      return newState;
    });
  }, []);

  // 使用useMemo优化组件渲染
  const SettingsPage = useMemo(() => memo(() => {
    const [openDialog, setOpenDialog] = useState(false);
    const [editDialogOpen, setEditDialogOpen] = useState(false);
    const [newKeyName, setNewKeyName] = useState('');
    const [newKeyValue, setNewKeyValue] = useState('');
    const [newKeyBaseUrl, setNewKeyBaseUrl] = useState('');
    const [editingKeyId, setEditingKeyId] = useState<string | null>(null);
    const { apiKeys, addApiKey, removeApiKey, updateApiKey } = useApiKeys();

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

    return (
      <Box 
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
  }), [handleSetCurrentPage, theme, toggleTheme]);

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
      <button 
        className={`voice-button ${isListening ? 'listening' : ''}`}
        onClick={toggleListening}
      >
        {isListening ? <MicOffIcon sx={{ fontSize: 50, color: 'white' }} /> : <MicIcon sx={{ fontSize: 50, color: 'white' }} />}
      </button>
      
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
  )), [handleSettingsClick, isListening, toggleListening, theme, toggleTheme]);

  if (currentPage === 'settings') {
    return <SettingsPage />;
  }

  return <HomePage />;
};

export default memo(App);