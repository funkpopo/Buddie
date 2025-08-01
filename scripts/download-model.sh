#!/bin/bash

# SenseVoice 模型下载脚本
# 此脚本会下载预训练的 SenseVoice 模型文件

MODEL_DIR="./models"
MODEL_URL="https://huggingface.co/lovemefan/sense-voice-gguf/resolve/main/sense-voice-small-q4_k.gguf"
MODEL_FILE="sense-voice-small-q4_k.gguf"

echo "正在下载 SenseVoice 模型..."
echo "模型文件: $MODEL_FILE"
echo "下载地址: $MODEL_URL"

# 创建模型目录
mkdir -p "$MODEL_DIR"

# 检查模型文件是否已存在
if [ -f "$MODEL_DIR/$MODEL_FILE" ]; then
    echo "模型文件已存在: $MODEL_DIR/$MODEL_FILE"
    echo "如需重新下载，请先删除现有文件"
    exit 0
fi

# 下载模型文件
if command -v wget &> /dev/null; then
    wget -O "$MODEL_DIR/$MODEL_FILE" "$MODEL_URL"
elif command -v curl &> /dev/null; then
    curl -L -o "$MODEL_DIR/$MODEL_FILE" "$MODEL_URL"
else
    echo "错误: 未找到 wget 或 curl 命令"
    echo "请手动下载模型文件到 $MODEL_DIR/$MODEL_FILE"
    echo "下载地址: $MODEL_URL"
    exit 1
fi

# 检查下载是否成功
if [ -f "$MODEL_DIR/$MODEL_FILE" ]; then
    echo "模型下载成功: $MODEL_DIR/$MODEL_FILE"
    echo "文件大小: $(du -h "$MODEL_DIR/$MODEL_FILE" | cut -f1)"
else
    echo "模型下载失败"
    exit 1
fi

echo "SenseVoice 模型下载完成!"