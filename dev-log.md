已经做好了人物基础状态机，起步停步，相机系统，动画系统关键帧设置那里不太好需要改


# 相机和输入功能冲突
26/9/18
## 发现问题
我发现我的相机系统里面用的cinemachine，主相机有Brain文件，第三人称相机也有CinemachineVirtualCamera，但是脚本里面又写的普通相机逻辑
好在我的cinemachine只管了跟随，但是滑动的太快又会出现抽搐的问题，大概率还是cinemachine自身和代码的转向有冲突

26/9/19
## 修改过程
**脚本**应该用**手写**的，用cinemachine根本没办法做简单的第三人称视角，cinemachine是用来做复杂相机切换的

所以现在第三人称相机完全由手写脚本控制，不过把inputsystem加进去了，变成单独的类了，**之前的inputsystem和cinemachine完全没办法配合做出第三人称简单相机**

**再也不瞎跟教程了，第三人称明明手写就很好了，非得和cinemachine放一起，根本塞不进去inputsystem**