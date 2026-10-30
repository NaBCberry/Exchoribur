# 音频时间戳测试素材

这两个小文件是自行生成的黑色视频与正弦音,不含任何外部节目素材。
测试只需随包的动态库,运行时不依赖 ffmpeg/ffprobe 命令行工具。

生成工具:本机 FFmpeg 命令行（仅用于制作测试输入）。

```powershell
ffmpeg -f lavfi -i "aevalsrc=if(between(t\,0.5\,2.5)\,0.6*sin(2*PI*1000*t)\,0):s=22050:d=3" tone.wav
ffmpeg -f lavfi -i "color=c=black:s=32x32:r=5:d=4" -itsoffset 0.75 -i tone.wav -map 0:v -map 1:a -c:v mpeg4 -q:v 10 -c:a aac -b:a 32k audio-delayed.mp4
ffmpeg -copyts -itsoffset 5 -i audio-delayed.mp4 -c copy audio-delayed-shifted.mp4
```

第一个文件画面从 0 秒开始,音轨延后 0.75 秒,可闻音调从时间轴 1.25 秒开始。
第二个文件把所有轨道的时间戳统一后移 5 秒;减去容器共同原点后,频谱仍应位于
相同时间。AAC 编码延迟由容器编辑列表及解码器处理,不能以首个压缩包归零。
