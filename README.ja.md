<p align="center">
  <img src="docs/images/hero.png" alt="EDS Wafer Sort Simulator 実行画面" width="100%">
</p>

<h1 align="center">EDS Wafer Sort Simulator</h1>

<p align="center">
  <img src="https://img.shields.io/badge/C%23-512BD4?logo=csharp&logoColor=white" alt="C#">
  <img src="https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white" alt=".NET 10">
  <img src="https://img.shields.io/badge/WinForms-Windows-0078D4?logo=windows&logoColor=white" alt="WinForms">
  <img src="https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4?logo=windows11&logoColor=white" alt="Windows 10/11">
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-35A853" alt="MIT License"></a>
</p>

<p align="center">
  <a href="README.md">한국어</a> · <a href="README.en.md">English</a> · 日本語
</p>

C# WinFormsで実装した半導体EDS Wafer Testシミュレーターです。Fab-outされたメモリおよびシステム半導体のWafer Lotを受け入れ、製品と互換性のあるATE Wafer Sort Test Cellを割り当て、25枚のWaferを順次テストする仮想OSAT Wafer Testプログラムです。

## 目次

- [Key Features](#key-features)
- [Quick Start](#quick-start)
- [使用例](#使用例)
- [Output Example](#output-example)
- [Workflow](#workflow)
- [Supported Test Lines](#supported-test-lines)
- [Architecture](#architecture)
- [Simulation Model](#simulation-model)
- [State & Recovery](#state--recovery)
- [v3 Catalogs](#v3-catalogs)
- [Output Structure](#output-structure)
- [Project Structure](#project-structure)
- [Testing](#testing)
- [Limitations](#limitations)
- [Credits & Disclaimer](#credits--disclaimer)
- [License](#license)

## Key Features

- **Catalog-driven Job作成**: JSONカタログによりProduct → Recipe → Test Cellの互換性を検証
- **2種類のEDS Line**: LPDDR5X Memory LineとAutomotive Mixed-Signal MCU Line
- **25-Wafer Lotの順次実行**: Wafer01からWafer25までをRecipeステップごとにシミュレーション
- **リアルタイム状態同期**: Jobカード、Test Cellカード、詳細・進行画面の進捗率と装置状態を更新
- **検査済みDie UI**: WaferおよびDie UIから不良Dieを一目で確認
- **Final Binと歩留まり分析**: Wafer/Lot歩留まり、Bin分布、Pareto、ステップ別失敗を集計
- **装置エラーシミュレーション**: Tester・Prober・Probe Cardのエラー、Run失敗、Cellエラー保持、手動リセットに対応
- **チェックポイントと復旧**: Wafer単位で保存し、異常終了を`Interrupted`として復旧、再実行履歴を保持
- **結果レポート自動生成**: 歩留まりと主な失敗要因を含む顧客向けPDFレポート、および統合ログを自動生成
- **Jobスナップショット**: Product・Recipe・Test Cellのカタログ変更後も既存Runの再現性を維持

## Quick Start

### 必要環境

- Windows 10またはWindows 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- 任意: Visual Studioの`.NET デスクトップ開発`ワークロード

### インストールと実行

```powershell
git clone https://github.com/youn9k/EDS-Wafer-Sort-Simulator.git
cd EDS-Wafer-Sort-Simulator
dotnet restore
dotnet build RecipeTestProject.slnx
dotnet run --project RecipeTestProject.csproj
```

### SelfTests

```powershell
dotnet run --project RecipeTestProject.SelfTests/RecipeTestProject.SelfTests.csproj
```

## 使用例

### 1. Test Cellの状態を確認

装置一覧でMemory/System IC Lineの接続状態、Tester、Prober、および現在のJobを確認します。

<p align="center">
  <img src="docs/images/equipment-list.png" alt="装置一覧画面" width="900">
</p>

### 2. Jobを作成

顧客名、依頼番号、Lot IDを入力し、`Product → Recipe → Test Cell`の順に選択します。互換性のないRecipeとTest Cellは自動的に除外されます。

<p align="center">
  <img src="docs/images/job-create.png" alt="Job作成画面" width="900">
</p>

### 3. 模擬EDS結果を設定

Lotの基本目標歩留まりと失敗Bin分布を設定し、必要に応じてWafer別の目標歩留まりまたは構成部品エラーを上書きします。

<p align="center">
  <img src="docs/images/simulation-settings.png" alt="模擬EDS結果設定画面" width="900">
</p>

### 4. 25-Wafer Lotを実行

Wafer01からWafer25まで順次実行し、現在のWafer、Recipeステップ、Cell状態、リアルタイムログを確認します。

<p align="center">
  <img src="docs/images/run-progress.png" alt="Lot進行画面" width="900">
</p>

### 5. 結果を分析

Lot歩留まり、Final Bin Pareto、25枚の結果表を確認し、Wafer別Die mapで不良位置とBinを分析します。

<p align="center">
  <img src="docs/images/result-summary.png" alt="Lot結果画面" width="900">
</p>

<p align="center">
  <img src="docs/images/wafer-detail.png" alt="Wafer詳細画面" width="900">
</p>

## Output Example

完了したRunでは、顧客・Lot・Product・Recipe・Test Cell情報、Lot/Testサマリー、25枚の結果表を含む顧客向けPDFレポートを自動生成します。Low Yield Waferがある場合は、そのFinal Bin mapを付録に追加します。

### レポートサマリー

<p align="center">
  <img src="docs/images/report-summary.png" alt="PDF結果レポートのサマリーページ" width="760">
</p>

### LotおよびTestサマリー

<p align="center">
  <img src="docs/images/report-test-summary.png" alt="PDF Testサマリー" width="900">
</p>

### Low Yield Wafer付録

<p align="center">
  <img src="docs/images/report-low-yield.png" alt="PDF Low Yield Wafer付録" width="760">
</p>

完了Runで自動保存されるファイル:

```text
Results/{JobId}/{RunId}/
├─ run-result.json
├─ run.log
└─ report.pdf
```

## Workflow

```mermaid
flowchart LR
    A["顧客依頼 / Lot受け入れ"] --> B["Product選択"]
    B --> C["互換Recipeのフィルタリング"]
    C --> D["接続済みTest Cell割り当て"]
    D --> E["Lot歩留まり・Bin分布設定"]
    E --> F["Wafer01～25順次実行"]
    F --> G["Final Bin map・Lot/Test集計"]
    G --> H["JSON・ログ・PDF自動保存"]
```

- JobはMemoryとSystem IC Lineを共通に表現する実行単位です。
- Test CellはTester、Wafer Prober、固定Probe Cardで構成されます。
- 1台のCellでは同時に1つのRunだけを実行でき、待機Jobは自動起動しません。
- Low Yieldは製品検査結果であるため次のWaferへ進み、CellエラーはRunを即時失敗にします。

## Supported Test Lines

| 項目 | Memory Line | System IC Line |
|---|---|---|
| Product | 300 mm LPDDR5X DRAM Wafer | 200 mm Automotive Mixed-Signal MCU Wafer |
| Tester | Advantest T5503HS2 | Teradyne J750Ex-HD |
| Prober | Tokyo Electron Prexa MS | Tokyo Electron Precio octo |
| Probe Card | LPDDR5X 300 mm Full-Wafer Probe Card | Automotive MCU 200 mm Multi-Site Probe Card |
| Recipe | Load/Contact → Continuity → Memory Cell → Read/Write → Timing Margin → Binning → Unload | Load/Contact → Continuity/DC → Digital/Scan → Embedded Memory → ADC/DAC → Binning → Unload |
| Final Bin | `PASS`, `CONTACT_FAIL`, `CELL_FAIL`, `READ_WRITE_FAIL`, `TIMING_FAIL` | `PASS`, `CONTACT_DC_FAIL`, `DIGITAL_FAIL`, `EMBEDDED_MEMORY_FAIL`, `MIXED_SIGNAL_FAIL` |

## Architecture

```mermaid
flowchart LR
    subgraph Catalog["v3 JSON Catalog"]
        P["Products"]
        R["Recipes"]
        C["Test Cells"]
    end

    P --> F["Compatibility Filter"]
    R --> F
    C --> F
    F --> J["Job Snapshot"]
    J --> S["Simulation Settings"]
    S --> L["Lot Test Runner"]
    C --> T["Live Test Cell State"]
    T --> L
    L --> K["Wafer Checkpoint"]
    K --> O["JSON / Log / PDF"]
    K --> U["Result UI"]
```

- 作成時にProduct・Recipe・Test CellをJobへスナップショット保存します。
- リアルタイムの接続・占有・エラー状態は`TestCellId`で現在のCellと関連付けます。
- 実行エンジンは`IProgress<T>`でUIへ進行状況を通知し、`CancellationToken`でキャンセルを処理します。

## Simulation Model

- 実際のdie幅・高さからグリッドを生成し、die中心が`wafer radius - edge exclusion`内にある場合のみ有効dieとして使用します。
- 目標歩留まりに最も近い整数のPASS die数を選択します。
- `Run ID + Wafer ID`による決定的seedで失敗die位置とFinal Binを生成します。
- 基本目標歩留まりは98%、4つの失敗Binの基本分布は各25%です。
- Waferの代表Binに失敗dieの60%を割り当て、残りはLot分布に従います。
- Wafer歩留まりがProduct基準の95%以上なら`Passed`、未満なら`LowYield`です。
- Lot歩留まりは完了した25枚全体の`PASS die合計 / 有効die合計`です。
- 製品Final BinとCellエラーは分離して保存します。

## State & Recovery

```mermaid
stateDiagram-v2
    [*] --> Pending
    Pending --> Running: ユーザーがRun開始
    Running --> Completed: 25枚のWafer完了
    Running --> Failed: Tester/Prober/Probe Cardエラー
    Running --> Canceled: ユーザーがキャンセル
    Running --> Interrupted: 異常終了から復旧
    Completed --> Running: 新規Run
    Failed --> Running: エラーリセット後に新規Run
    Canceled --> Running: 新規Run
    Interrupted --> Running: Wafer01から新規Run
```

- 各Wafer完了時にJob、部分結果JSON、ログをチェックポイント保存します。
- 起動時に`Running`のまま残ったRunは`Interrupted`へ変更し、完了Waferを保持します。
- 再実行は過去のRunを変更せず、Wafer01から新しいRunを作成します。
- CellエラーはRun終了後も保持され、装置詳細画面の`エラーリセット`でのみ解除されます。
- Job削除はメタデータだけを削除し、生成済み結果ファイルは保持します。

## v3 Catalogs

### Product

```json
{
  "schemaVersion": "3.0",
  "productId": "LPDDR5X-300",
  "family": "Memory",
  "name": "300 mm LPDDR5X DRAM Wafer",
  "waferDiameterMm": 300,
  "dieWidthMm": 12.0,
  "dieHeightMm": 8.0,
  "edgeExclusionMm": 3.0,
  "acceptanceYieldPercent": 95.0,
  "allowedRecipeIds": ["LPDDR5X-EDS-PROD"]
}
```

### Recipe

```json
{
  "schemaVersion": "3.0",
  "recipeId": "LPDDR5X-EDS-PROD",
  "productFamily": "Memory",
  "compatibleTestCellIds": ["MEM-CELL-01"],
  "requiredCapabilities": ["LPDDR5X", "300mm", "FullWaferContact", "HighSpeedMemory"],
  "finalBins": [
    { "code": "PASS", "isPass": true, "colorHex": "#27AE60" },
    {
      "code": "CELL_FAIL",
      "isPass": false,
      "relatedStepId": "CELL_TEST",
      "colorHex": "#E74C3C"
    }
  ]
}
```

### Test Cell

```json
{
  "schemaVersion": "3.0",
  "id": "MEM-CELL-01",
  "line": "Memory Line",
  "tester": {
    "manufacturer": "Advantest",
    "model": "T5503HS2",
    "imageFile": "images/advantest-t5503hs2.png"
  },
  "prober": {
    "manufacturer": "Tokyo Electron",
    "model": "Prexa MS",
    "imageFile": "images/tel-prexa-ms.jpg"
  },
  "supportedWaferDiametersMm": [300],
  "capabilities": ["LPDDR5X", "300mm", "FullWaferContact", "HighSpeedMemory"]
}
```

従来のv1/v2光学検査ドキュメントには対応しません。不正なJSON、重複ID、必須項目の欠落、Product/Recipe/Cellの関係またはcapability不一致は、起動時の警告にファイル単位で表示されます。

## Output Structure

実行データは`%LocalAppData%\RecipeTestProject`に保存されます。

```text
RecipeTestProject/
├─ test-cell-state.json
├─ jobs.json
└─ Results/
   └─ {JobId}/{RunId}/
      ├─ run-result.json
      ├─ run.log
      └─ report.pdf
```

- JobとRunのチェックポイントJSONは一時ファイルへ書き込んだ後に置換します。
- Completed RunではJSON・ログ・PDFを保存します。
- Failed/Canceled/Interrupted Runでは部分JSONとログのみ保持します。
- 結果ファイルが欠落または破損してもRun履歴と保存パスは維持します。

## Project Structure

```text
EDS-Wafer-Sort-Simulator/
├─ Products/                         # Product v3 catalog
├─ Recipes/                          # Recipe v3 catalog
├─ equipment/                        # Test Cell catalog and equipment images
├─ docs/images/                      # README UI and PDF previews
├─ RecipeTestProject.SelfTests/      # Domain, runner, storage, and PDF self-tests
├─ Models.cs                         # Domain and persistence models
├─ Services.cs                       # Catalog, runner, storage, and recovery services
├─ UiComponents.cs                   # WinForms screens and reusable controls
├─ ReportService.cs                  # PDF report generation
└─ RecipeTestProject.slnx
```

## Testing

SelfTestsでは次のシナリオを検証します。

- Product/Recipe/Test Cell v3必須項目、関係、capability互換性
- Product → Recipe → Test Cellフィルター
- 動的300/200 mm die geometryと95%合格境界
- 98%基本歩留まり、均等Bin分布、代表Bin 60%
- 決定的な結果生成と25-Wafer Lot完了
- Tester・Prober・Probe Cardエラー、キャンセル、手動エラーリセット
- Cell接続・占有状態と同時実行の防止
- Waferチェックポイント、欠落結果、既存データ保持
- JSON・ログ・PDF生成とLow Yield map付録

## Limitations

- 対象はFab-out WaferのEDS電気的選別フローに限定されます。
- Burn-in、実際のRepair、Package Final Test、Module/SLTは含みません。
- 実際のATE/Prober通信プロトコルとハードウェア制御は実装していません。
- 歩留まり、Final Bin、エラー結果はポートフォリオ目的の決定的シミュレーションデータです。
- TesterとProberの組み合わせは実製品を参考にしていますが、Probe Cardとネットワーク接続は仮想構成です。

## Credits & Disclaimer

装置カードには各メーカーが公開している製品写真を組み合わせて使用しています。製品名、商標、写真の権利は各メーカーに帰属し、本プロジェクトは各メーカーとの提携または保証を受けたものではありません。

- [Advantest T5503HS2](https://www.advantest.com/tw/products/semiconductor-test-system/memory/t5503hs2/)
- [Teradyne J750Ex-HD](https://www.teradyne.com/products/j750/?lang=en)
- [Tokyo Electron Prexa MS](https://www.tel.com/product/prexa.html)
- [Tokyo Electron Precio octo](https://www.tel.com/product/precio.html)

## License

ソースコードは[MIT License](LICENSE)で配布されます。

メーカーの装置写真、製品名、商標はMIT Licenseの対象外であり、それぞれの権利者に帰属します。
