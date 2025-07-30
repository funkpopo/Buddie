import React, { useState, useCallback, memo, useMemo, useRef } from 'react';
import { 
  Fab, 
  IconButton, 
  Menu, 
  MenuItem, 
  Box, 
  Typography,
  keyframes
} from '@mui/material';
import { 
  Settings as SettingsIcon, 
  Home as HomeIcon
} from '@mui/icons-material';

// 定义流光色彩动画
const gradientAnimation = keyframes`
  0% {
    background-position: 0% 50%;
    box-shadow: 0 0 20px rgba(255, 107, 107, 0.8);
  }
  25% {
    box-shadow: 0 0 40px rgba(78, 205, 196, 1);
  }
  50% {
    background-position: 100% 50%;
    box-shadow: 0 0 60px rgba(69, 183, 209, 1);
  }
  75% {
    box-shadow: 0 0 40px rgba(150, 206, 180, 1);
  }
  100% {
    background-position: 0% 50%;
    box-shadow: 0 0 20px rgba(255, 107, 107, 0.8);
  }
`;

// 定义脉冲动画
const pulseAnimation = keyframes`
  0% {
    transform: scale(0.9);
    box-shadow: 0 0 0 0 rgba(255, 107, 107, 0.9);
  }
  50% {
    transform: scale(1.05);
    box-shadow: 0 0 0 20px rgba(255, 107, 107, 0.3);
  }
  100% {
    transform: scale(0.9);
    box-shadow: 0 0 0 40px rgba(255, 107, 107, 0);
  }
`;

// 定义旋转边框动画
const rotateBorder = keyframes`
  0% {
    transform: rotate(0deg);
    filter: hue-rotate(0deg);
  }
  100% {
    transform: rotate(360deg);
    filter: hue-rotate(360deg);
  }
`;

// 定义粒子动画
const particleAnimation = keyframes`
  0% {
    transform: translate(0, 0) rotate(0deg);
    opacity: 1;
  }
  100% {
    transform: translate(var(--tx), var(--ty)) rotate(720deg);
    opacity: 0;
  }
`;

// 定义浮动动画
const floatAnimation = keyframes`
  0% {
    transform: translateY(0px);
  }
  50% {
    transform: translateY(-20px);
  }
  100% {
    transform: translateY(0px);
  }
`;

const App: React.FC = () => {
  const [currentPage, setCurrentPage] = useState<'home' | 'settings'>('home');
  const [anchorEl, setAnchorEl] = useState<null | HTMLElement>(null);
  const open = Boolean(anchorEl);
  const fabRef = useRef<HTMLButtonElement>(null);

  const handleSetCurrentPage = useCallback((page: 'home' | 'settings') => {
    setCurrentPage(page);
  }, []);

  const handleClick = useCallback((event: React.MouseEvent<HTMLElement>) => {
    setAnchorEl(fabRef.current);
  }, []);

  const handleSettingsClick = useCallback(() => {
    handleSetCurrentPage('settings');
  }, [handleSetCurrentPage]);

  const handleClose = useCallback(() => {
    setAnchorEl(null);
  }, []);

  const handleMenuClick = useCallback((action: string) => {
    console.log(`执行功能: ${action}`);
    handleClose();
  }, [handleClose]);

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
  )), [handleSetCurrentPage]);

  const HomePage = useMemo(() => memo(() => (
    <Box 
      sx={{ 
        height: '100vh', 
        width: '100vw',
        display: 'flex', 
        flexDirection: 'column',
        position: 'relative'
      }}
    >
      {/* 中央大圆形按钮容器 */}
      <Box 
        sx={{ 
          display: 'flex', 
          justifyContent: 'center', 
          alignItems: 'center',
          flexGrow: 1,
          position: 'relative',
          animation: `${floatAnimation} 3s ease-in-out infinite`
        }}
      >
        {/* 粒子效果容器 */}
        <Box
          sx={{
            position: 'absolute',
            width: 600,
            height: 600,
            pointerEvents: 'none',
            zIndex: 1
          }}
        >
          {Array.from({ length: 30 }).map((_, i) => (
            <Box
              key={i}
              sx={{
                position: 'absolute',
                top: '50%',
                left: '50%',
                width: 10,
                height: 10,
                borderRadius: '50%',
                background: `hsl(${Math.random() * 360}, 70%, 60%)`,
                opacity: 0,
                animation: `${particleAnimation} ${2 + Math.random() * 3}s linear infinite`,
                '--tx': `${(Math.random() - 0.5) * 500}px`,
                '--ty': `${(Math.random() - 0.5) * 500}px`,
                animationDelay: `${Math.random() * 5}s`
              }}
            />
          ))}
        </Box>
        
        {/* 旋转边框效果 */}
        <Box
          sx={{
            position: 'absolute',
            width: 450,
            height: 450,
            borderRadius: '50%',
            background: 'conic-gradient(transparent, #FF6B6B, #4ECDC4, #45B7D1, #96CEB4, transparent)',
            animation: `${rotateBorder} 2s linear infinite`,
            filter: 'blur(8px)',
            opacity: 0.8,
            zIndex: 0
          }}
        />
        
        {/* 内层旋转边框 */}
        <Box
          sx={{
            position: 'absolute',
            width: 480,
            height: 480,
            borderRadius: '50%',
            background: 'conic-gradient(transparent, #FFEAA7, #DDA0DD, #FF6B6B, transparent)',
            animation: `${rotateBorder} 3s linear infinite reverse`,
            filter: 'blur(6px)',
            opacity: 0.6,
            zIndex: 0
          }}
        />
        
        <Fab 
          aria-label="add"
          sx={{ 
            width: 400, 
            height: 400,
            background: 'radial-gradient(circle at 30% 30%, #FF6B6B, #4ECDC4, #45B7D1, #96CEB4)',
            backgroundSize: '400% 400%',
            animation: `${gradientAnimation} 3s ease infinite, ${pulseAnimation} 2s infinite`,
            border: 'none',
            position: 'relative',
            overflow: 'hidden',
            borderRadius: '50%',
            zIndex: 2,
            boxShadow: '0 0 40px rgba(255, 107, 107, 0.8), 0 0 80px rgba(78, 205, 196, 0.6)',
            '&::before': {
              content: '""',
              position: 'absolute',
              top: '-50%',
              left: '-50%',
              width: '200%',
              height: '200%',
              background: 'radial-gradient(circle, rgba(255,255,255,0.5) 0%, rgba(255,255,255,0) 70%)',
              transform: 'rotate(30deg)',
              opacity: 0.8,
              zIndex: 3
            },
            '&::after': {
              content: '""',
              position: 'absolute',
              top: '15px',
              left: '15px',
              right: '15px',
              bottom: '15px',
              background: 'inherit',
              borderRadius: '50%',
              zIndex: 1,
              filter: 'blur(15px)',
              opacity: 0.9
            },
            '&:hover': {
              animation: `${gradientAnimation} 1.5s ease infinite, ${pulseAnimation} 1s infinite`,
              transform: 'scale(1.1)',
              boxShadow: '0 0 70px rgba(255, 107, 107, 1), 0 0 140px rgba(78, 205, 196, 0.8)',
              '&::before': {
                opacity: 1,
                transform: 'rotate(90deg)'
              }
            },
            transition: 'transform 0.4s ease, box-shadow 0.4s ease'
          }}
        />
      </Box>
      
      {/* 设置按钮 - 左上角 */}
      <IconButton
        onClick={handleSettingsClick}
        sx={{
          position: 'absolute',
          top: 16,
          left: 16,
          zIndex: 1000,
        }}
      >
        <SettingsIcon sx={{ fontSize: 36 }} />
      </IconButton>
    </Box>
  )), [handleSettingsClick]);

  if (currentPage === 'settings') {
    return <SettingsPage />;
  }

  return <HomePage />;
};

export default memo(App);