# Raid Buff Off

國際服 Dalamud API 15 插件，僅在已勾選絕境戰的戰鬥中，取消自己身上已勾選、可自行取消的團隊增益。

## DTR bar

顯示 raidbuff: ON / raidbuff: OFF。左鍵切換總開關，右鍵開啟詳細設定。ON 代表已啟用，未進入已勾選絕境戰的戰鬥時保持待命。

## 職業增益清單

- 占星術士 AST：Divination、The Balance、The Spear。
- 武僧 MNK：Brotherhood。
- 龍騎士 DRG：Battle Litany。
- Reaper RPR：Arcane Circle。
- 吟遊詩人 BRD：Battle Voice、Radiant Finale。
- 舞者 DNC：Technical Finish、Devilment。
- 召喚師 SMN：Searing Light。
- 赤魔法師 RDM：Embolden。
- Pictomancer PCT：Starry Muse。

各職業分組以 checkbox 勾選，每橫排三個職業；保留增益搜尋，不提供「只顯示已勾選」或「只顯示目前身上的增益」篩選；同名版本集中在一個勾選項目。清單使用已核對的 PvE 狀態 ID，並檢查遊戲名稱及正面增益分類；不會混入同名的舊版或 PvP 狀態。CanStatusOff 欄位不足以單獨判定這份 PvE 清單。每次取消仍由遊戲判定，回傳拒絕時不視為成功。沒有「所有增益」選項。圖片巨集中 Starry Mues 的拼字以正式名稱 Starry Muse 為準。

## 生效場景

場景清單僅保留七個絕境戰：

| 簡稱 | 內容 | Territory ID |
|---|---|---|
| UCOB | The Unending Coil of Bahamut (Ultimate) | 733 |
| UWU | The Weapon's Refrain (Ultimate) | 777 |
| TEA | The Epic of Alexander (Ultimate) | 887 |
| DSR | Dragonsong's Reprise (Ultimate) | 968 |
| TOP | The Omega Protocol (Ultimate) | 1122 |
| FRU | Futures Rewritten (Ultimate) | 1238 |
| UMAD | Dancing Mad (Ultimate) | 1363 |

需要同時滿足總開關 ON、目前絕境戰已勾選、角色在戰鬥中且可用。戰鬥外不執行。預設 OFF，增益與場景全不勾選；設定自動保存。從舊版更新會清除已移出清單的 Buff 和一般場景勾選。

## 安裝

自訂插件庫：

```text
https://raw.githubusercontent.com/WayneXXDD/DalamudPlugins/main/pluginmaster.json
```

開啟 Get plugin testing builds，刷新並搜尋 Raid Buff Off。輸入 /rbo 或在 DTR 上按右鍵開設定。也支援 /rbo on、/rbo off、/rbo toggle。

## 驗證與發布

GitHub Actions 執行生效條件、清單範圍、舊設定遷移及頻率限制測試，並使用 [Global 遊戲資料](https://github.com/xivapi/ffxiv-datamining/tree/master/csv/en) 驗證 PvE 狀態 ID／名稱與七個絕境戰對照。編譯後檢查 ZIP 並發布到 plugin-repo 分支。

使用遊戲 ExecuteStatusOff 操作，只取消自己的狀態，不直接刪除狀態記憶體。編譯和資料驗證不能取代遊戲內確認，先以 Testing 測試版發布。

MIT，詳見 LICENSE。
