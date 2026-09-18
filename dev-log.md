已经做好了人物基础状态机，起步停步，相机系统，动画系统关键帧设置那里不太好需要改

26/9/18
**发现问题**
我发现我的相机系统里面用的cinemachine，主相机有Brain文件，第三人称相机也有CinemachineVirtualCamera，但是脚本里面又写的普通相机逻辑
好在我的cinemachine只管了跟随，但是滑动的太快又会出现抽搐的问题，大概率还是cinemachine自身和代码的转向有冲突


