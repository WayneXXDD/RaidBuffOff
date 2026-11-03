# Raid Buff Off

國際服 Dalamud API 15 的獨立插件，用來自動取消自己身上已勾選的增益。

## 生效條件

三個條件必須同時成立：

1. 總開關為 ON。
2. 目前副本／區域已在「生效場景」勾選。
3. 角色正在戰鬥中，且已載入、存活並未處於過場。

預設 OFF，Buff 與場景清單都不勾選。設定會自動保存。ON 代表功能已啟用，戰鬥外或不在所選場景時仍會待命。

## DTR bar

- 顯示 BuffOff: ON / BuffOff: OFF。
- 左鍵切換 ON／OFF。
- 右鍵直接開啟詳細設定。
- 滑鼠提示顯示目前是生效中或待命，以及最近取消的增益。

## 設定

輸入 /rbo，或在 DTR 項目上按右鍵。

「要移除的增益」：以 checkbox 選擇。預設顯示常見團隊增益；可切換「所有可自行取消的增益」，並搜尋名稱、英文名稱或狀態 ID。也可以只顯示目前身上有的或已勾選的增益。同名版本集中為一個勾選項目；滑鼠提示顯示所有狀態 ID。

「生效場景」：搜尋副本／區域或場景 ID，以 checkbox 勾選；也可以直接勾選「在目前場景的戰鬥中生效」。

清單只顯示遊戲資料標記為可自行取消的正面增益。插件只針對本機角色自己的狀態，使用遊戲的 ExecuteStatusOff 操作，保留遊戲對取消資格的檢查；不直接刪除狀態記憶體。

## 安裝

在 Dalamud 自訂庫加入：

```text
https://raw.githubusercontent.com/WayneXXDD/DalamudPlugins/main/pluginmaster.json
```

本插件先以 Testing 測試版發布。開啟 Get plugin testing builds，刷新插件清單並搜尋 Raid Buff Off。

指令：/rbo 開設定；/rbo on、/rbo off、/rbo toggle 切換總開關。

## 建置與發布

GitHub Actions 執行純核心測試、使用國際服 Dalamud API 15 編譯、驗證 ZIP 內容與版本，再發布到 plugin-repo 分支。下載檔與中央插件庫自動同步；本機不需要保存開發專案。

測試驗證生效條件、勾選清單、來源 ID 和取消頻率。DTR 互動與實際取消效果仍需遊戲內確認。

MIT 授權，詳見 LICENSE。
