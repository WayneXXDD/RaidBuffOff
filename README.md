# Raid Buff Off

國際服 Dalamud API 15 插件，在已勾選絕境戰的戰鬥中取消自己身上已勾選的團隊增益。可另勾選「測試區域：所有區域」在其他區域戰鬥中測試。

## DTR bar

顯示 raidbuff: ON / raidbuff: OFF。左鍵切換總開關，右鍵開啟詳細設定。ON 代表已啟用，未進入已勾選絕境戰的戰鬥時保持待命。

## 職業增益清單

- Astrologian AST：Divination、The Balance、The Spear。
- Monk MNK：Brotherhood。
- Dragoon DRG：Battle Litany。
- Reaper RPR：Arcane Circle。
- Bard BRD：Battle Voice、Radiant Finale。
- Dancer DNC：Technical Finish、Devilment。
- Summoner SMN：Searing Light。
- Red Mage RDM：Embolden。

職業標題全部使用英文、固定展開，沒有收合箭頭。每橫排三個職業，各增益以 checkbox 勾選；保留增益搜尋，不提供「只顯示已勾選」或「只顯示目前身上的增益」篩選；同名版本集中在一個勾選項目。清單使用已核對的 PvE 狀態 ID，並檢查遊戲名稱及正面增益分類；不會混入同名的舊版或 PvP 狀態。CanStatusOff 欄位不足以單獨判定這份 PvE 清單。每次取消仍由遊戲判定，回傳拒絕時不視為成功。沒有「所有增益」選項。Starry Muse 已移除，不會出現在清單中，也不會再對它執行取消；舊版對它的勾選會自動清除。

## 生效場景

場景頁最上方提供「測試區域：所有區域（僅戰鬥中生效）」checkbox，預設不勾選。啟用後略過區域限制，但仍需總開關 ON、角色可用、正在戰鬥且 Buff 已勾選；關閉後立即回到已勾選絕境戰的限制。

一般場景清單保留七個絕境戰：

| 簡稱 | 內容 | Territory ID |
|---|---|---|
| UCOB | The Unending Coil of Bahamut (Ultimate) | 733 |
| UWU | The Weapon's Refrain (Ultimate) | 777 |
| TEA | The Epic of Alexander (Ultimate) | 887 |
| DSR | Dragonsong's Reprise (Ultimate) | 968 |
| TOP | The Omega Protocol (Ultimate) | 1122 |
| FRU | Futures Rewritten (Ultimate) | 1238 |
| UMAD | Dancing Mad (Ultimate) | 1363 |

未啟用測試區域時，需要同時滿足總開關 ON、目前絕境戰已勾選、角色在戰鬥中且可用。戰鬥外不執行。預設 OFF，增益與場景全不勾選；設定自動保存。從舊版更新會清除已移出清單的 Buff 和一般場景勾選。

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
