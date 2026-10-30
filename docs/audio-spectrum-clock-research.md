# 音频频谱时间基准调查

已放弃 LibVLC smem 的实验解析实现,改用独立的 FFmpeg 解复用与解码接口。
以下记录为何不能使用 `smem PTS - Playing 回调时钟` 换算媒体时间。

## 已确认的来源

核对与当前依赖对应的 VLC 3.0.23 源码:

1. `src/input/decoder.c` 的 `DecoderPlaySout` 在发送转码输出前调用
   `DecoderFixTs`,处理音视频轨道的时间延迟并进入时钟转换。
2. `src/input/clock.c` 的 `input_clock_ConvertTS` 调用 `ClockStreamToSystem`,
   使用流参考点、系统参考点及播放速率,并加入漂移平均值、PTS 延迟和时间偏移。
3. `modules/stream_out/smem.c` 的 `SendAudio` 将收到的块时间戳传给用户回调。
   这里没有恢复容器中的原始媒体时间。

因此,回调中的时间戳可以带有较大的单调时钟基数,不代表媒体有同样长度的片头。
它也不是 UTC 日期,不能用当前墙上时间来换算。

`Playing` 是播放器状态事件,并非内部流/系统参考点的导出接口。
回调中读取当前时钟不包含完整的参考点、延迟与漂移参数;不能认为简单相减就是
精确逆变换。对单个样本试出来的偏移不应推广到其他文件。

## 与其他误差区分

- 音轨晚于视频开始是文件本身的时间关系,必须保留,不能把第一包音频直接归零。
- 解码与显示时间可能不同,不能用首个压缩视频包的 PTS 推断整个文件的时间零点。
- 重采样包的样本数取整会导致按样本数累计的时间与包时间逐渐分离;
  这与系统时钟基数不同,不能用同一个固定偏移修正。
- FFT 窗口中心、步进和显示聚合带来的时间分辨率,必须与解码基准误差分开验收。

## 采用的方案与验收条件

优先取得解复用/解码阶段的媒体时间戳及其时间基,使用与预览一致的容器时间原点。
音轨起始时间用于保留真实偏移,不能分别把音视频各自归零。
LibVLC 3 的现有公共输出接口不能提供所需逆变换参数,因此改用
libavformat/libavcodec/libswresample,而不继续增加经验偏移。

验证应包括零偏移与已知延迟、不同封装、音视频重排和编码延迟、不同启动负载,
以及长音轨的开头/中间/结尾。多次解析同一文件的媒体时间映射必须与启动时刻无关。

## 一手资料

- [VLC 3.0.23 decoder.c](https://github.com/videolan/vlc/blob/3.0.23/src/input/decoder.c)
- [VLC 3.0.23 clock.c](https://github.com/videolan/vlc/blob/3.0.23/src/input/clock.c)
- [VLC 3.0.23 smem.c](https://github.com/videolan/vlc/blob/3.0.23/modules/stream_out/smem.c)
- [VLC 时钟架构说明中的旧时钟系统](https://github.com/videolan/vlc/blob/master/doc/clock.md)
  （该文介绍 VLC 4 的新架构,其中旧系统描述仅作为背景,具体行为以 3.0.23 源码为准。）
