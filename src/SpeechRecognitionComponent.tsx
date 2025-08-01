import React, { useState, useEffect, useCallback } from 'react';
import { Box, Button, Typography, Paper, CircularProgress } from '@mui/material';
import { Mic, MicOff } from '@mui/icons-material';

const SpeechRecognitionComponent: React.FC = () => {
  const [isListening, setIsListening] = useState(false);
  const [transcript, setTranscript] = useState('');
  const [isProcessing, setIsProcessing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // 处理语音识别结果
  const handleRecognitionResult = useCallback((result: any) => {
    if (result.text) {
      setTranscript(result.text);
    }
  }, []);

  // 开始语音识别
  const startListening = useCallback(async () => {
    setIsProcessing(true);
    setError(null);
    
    try {
      const result = await window.electronAPI.speechRecognition.start();
      if (result.success) {
        setIsListening(true);
      } else {
        setError(result.error || 'Failed to start speech recognition');
      }
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setIsProcessing(false);
    }
  }, []);

  // 停止语音识别
  const stopListening = useCallback(async () => {
    setIsProcessing(true);
    
    try {
      const result = await window.electronAPI.speechRecognition.stop();
      if (result.success) {
        setIsListening(false);
      } else {
        setError(result.error || 'Failed to stop speech recognition');
      }
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setIsProcessing(false);
    }
  }, []);

  // 切换语音识别状态
  const toggleListening = useCallback(() => {
    if (isListening) {
      stopListening();
    } else {
      startListening();
    }
  }, [isListening, startListening, stopListening]);

  // 设置事件监听器
  useEffect(() => {
    // 监听语音识别结果
    window.electronAPI.speechRecognition.onResult(handleRecognitionResult);
    
    // 监听语音识别错误
    window.electronAPI.speechRecognition.onError((errorMessage: string) => {
      setError(errorMessage);
      setIsListening(false);
      setIsProcessing(false);
    });
    
    // 清理函数
    return () => {
      // 如果正在监听，停止监听
      if (isListening) {
        stopListening();
      }
    };
  }, [handleRecognitionResult, isListening, stopListening]);

  return (
    <Paper elevation={3} sx={{ p: 3, mt: 3 }}>
      <Typography variant="h5" gutterBottom>
        语音识别
      </Typography>
      
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 2, mb: 2 }}>
        <Button
          variant="contained"
          color={isListening ? "secondary" : "primary"}
          startIcon={isListening ? <MicOff /> : <Mic />}
          onClick={toggleListening}
          disabled={isProcessing}
        >
          {isProcessing ? (
            <CircularProgress size={24} sx={{ color: 'inherit' }} />
          ) : isListening ? (
            '停止监听'
          ) : (
            '开始监听'
          )}
        </Button>
      </Box>
      
      {error && (
        <Typography color="error" sx={{ mb: 2 }}>
          错误: {error}
        </Typography>
      )}
      
      <Box sx={{ mt: 3 }}>
        <Typography variant="h6" gutterBottom>
          识别结果:
        </Typography>
        <Paper 
          variant="outlined" 
          sx={{ 
            p: 2, 
            minHeight: 100, 
            backgroundColor: '#f5f5f5',
            whiteSpace: 'pre-wrap',
            wordBreak: 'break-word'
          }}
        >
          {transcript || (isListening ? '正在监听...' : '点击"开始监听"按钮开始语音识别')}
        </Paper>
      </Box>
    </Paper>
  );
};

export default SpeechRecognitionComponent;