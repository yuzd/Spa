## 前端容器功能一览：
- 前端应用开发完后打包后自助上传部署
- 配合服务端脚本实现服务端渲染SSR功能
- 可以快速回滚到上一个版本
- 可以设置环境变量供SSR功能使用
- 服务端脚本提供log redis db http四大插件打造强大的基于js的ssr服务端运行脚本功能
- 服务端js脚本编辑器有智能代码提示
- 权限管理:轻量级csbin权限RABC
- Docker快速启动服务,一行命令搞定一个前端web容器

## 本方案的目的

服务端渲染 前端开发是没办法独立完成的，（除非用nodejs，但是还得要搭建nodejs的服务端 对于前端开发不仅要会nodejs后端技能，还得每次发布部署都是比较麻烦），基于我这套方案，只需要一次部署 ，这个系统给前端使用，可以独立完成服务端渲染。前端只需要会些js html css ，精力放在业务上就好了。

## 快速部署，各功能介绍使用 请查看wiki  https://github.com/yuzd/Spa/wiki

## 截图介绍


### 首页
![image](https://images4.c-ctrip.com/target/zb0g1d000001eeg3h59E0.png)


### 新建前端应用
![image](https://images4.c-ctrip.com/target/zb0j1d000001ed9vpCE40.png)

![image](https://images4.c-ctrip.com/target/zb091d000001eg5teF67C.png)

### 重新部署上传，快速回滚到上一个上传版本
![image](https://images4.c-ctrip.com/target/zb0d1d000001eca8g5E55.png)

### 全局配置
![image](https://images4.c-ctrip.com/target/zb0a1d000001ef32eC2D8.png)

### 服务端JS脚本编辑器,有智能提示代码
![image](https://images4.c-ctrip.com/target/zb0h1d000001eleyd2B05.png)

### 日志查看
![image](https://images4.c-ctrip.com/target/zb0s1d000001ekn161874.png)

### 权限管理
![image](https://dimg04.c-ctrip.com/images/0v52m120009fxw6gcB3A4.png)

### 接下来要做的
- 可以查看发布记录,有发布时间，发布人员，发布内容，可以查看和上一个版本diff,有哪些文件内容有变动