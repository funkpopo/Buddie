import React, { useState, useCallback, memo, useMemo } from 'react';
import { 
  IconButton, 
  Box, 
  Typography,
  Switch
} from '@mui/material';
import { 
  Settings as SettingsIcon, 
  Home as HomeIcon,
  Mic as MicIcon,
  MicOff as MicOffIcon,
  Brightness4 as DarkModeIcon,
  Brightness7 as LightModeIcon
} from '@mui/icons-material';
import { useTheme } from './ThemeContext';

const App: React.FC = () => {
  const { theme, toggleTheme } = useTheme();
  const [currentPage, setCurrentPage] = useState<'home' | 'settings'>('home');
  const [isListening, setIsListening] = useState<boolean>(false);

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
  const SettingsPage = useMemo(() => memo(() => (
    <Box 
      sx={{ 
        height: '100vh', 
        width: '100vw',
        display: 'flex', 
        flexDirection: 'column',
        position: 'relative'
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
          justifyContent: 'center',
          flexGrow: 1,
          pt: 8 // 为顶部按钮留出空间
        }}
      >
        <Typography variant="h4" sx={{ mb: 4 }}>设置页面</Typography>
        <Box sx={{ textAlign: 'center' }}>
          <Typography variant="h6">这里是设置页面内容</Typography>
          <Typography>你可以在这里添加各种设置选项</Typography>
        </Box>
      </Box>
    </Box>
  )), [handleSetCurrentPage, theme, toggleTheme]);

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