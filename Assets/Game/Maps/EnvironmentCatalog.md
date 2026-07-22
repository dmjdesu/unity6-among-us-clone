# Environment Catalog

調査日: 2026-07-20

## 調査対象

- 指示: 新しくインポートされたSF環境素材を調査し、まだマップ全体は作成しない
- 検出パッケージ: `Assets/KrishnaPalacio/MINIFANTASY - Forgotten Plains`
- 共通スクリプト: `Assets/KrishnaPalacio/MinifantasyCommon`
- `Assets/ThirdParty`: 空のまま
- 原本変更: なし

重要: 今回検出した新規素材はSF環境ではなく、2Dファンタジー屋外向けのTilemap素材です。宇宙船内部の床、壁、廊下、ドア、照明、大型設備Prefabは含まれていません。

## 追加アセット概要

| 種別 | 件数 | 内容 |
| --- | ---: | --- |
| Prefab | 15 | Props 6件、Tile Palette 9件 |
| Mesh / Model | 0 | `.fbx`, `.obj`, `.dae`, `.blend` は未検出 |
| Material | 0 | `.mat` は未検出。SpriteRendererはUnity組み込みDefault-Sprite参照 |
| Texture / Sprite | 13 | Tile, River, Waterfall, Props, Shadow系PNG |
| Demo Scene / Sample Scene | 3 | Forgotten Plains系Demo Scene |
| Tile / Rule / Animated Tile asset | 500 | RuleTile、AnimatedTile、標準Tileなど |

## Texture / Sprite

| Texture | 用途 |
| --- | --- |
| `Sprites/Tileset/Tiles.png` | 地面、壁、崖、湖などの基本タイル |
| `Sprites/Tileset/TilesExtras.png` | 追加タイル装飾 |
| `Sprites/Tileset/TilesShadows.png` | タイル影 |
| `Sprites/Tileset/RedRuleTiles.png` | Rule Tile確認用の赤タイル |
| `Sprites/Tileset/River/River*.png` | 川、曲線、湖接続 |
| `Sprites/Tileset/River/Waterfall/*.png` | 滝の地面、落下、水しぶきレイヤー |
| `Sprites/Props/Props.png` | Tree、Pillar、Flower、Cattail、Creeper、GrassLong |
| `Sprites/Props/PropsShadow.png` | Props用影 |

## Prefab一覧

| Prefab名 | 用途 | Bounds / Renderer Size | 推奨グリッドサイズ | 使用マテリアル | Collider | 分類 | トップダウン使用 | URPエラー |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `Cattail` | 水辺の草 | 1.000 x 1.000 | 0.125 | Built-in Default-Sprite | なし | 装飾 | 可 | ファイル上の異常なし |
| `Creeper` | つる植物 | 1.000 x 1.000 | 0.125 | Built-in Default-Sprite | なし | 装飾 | 可 | ファイル上の異常なし |
| `Flower` | 花 | 1.000 x 1.000 | 0.125 | Built-in Default-Sprite | なし | 装飾 | 可 | ファイル上の異常なし |
| `GrassLong` | 長い草 | 1.000 x 1.000 | 0.125 | Built-in Default-Sprite | なし | 装飾 | 可 | ファイル上の異常なし |
| `Pillar` | 石柱 | 1.000 x 1.000 / 1.375 x 2.125 | 0.125 | Built-in Default-Sprite | BoxCollider2D x1 | 家具/障害物 | 可 | ファイル上の異常なし |
| `Tree` | 木 | 2.875 x 3.375 / 2.625 x 3.125 | 0.125 | Built-in Default-Sprite | BoxCollider2D x1 | 大型装飾/障害物 | 可 | ファイル上の異常なし |
| `FP Ground` | Ground標準Tile Palette | Palette | 0.125 | Tilemap default | Scene依存 | 床 | 可 | ファイル上の異常なし |
| `FP Wall` | Wall標準Tile Palette | Palette | 0.125 | Tilemap default | Scene依存 | 壁 | 可 | ファイル上の異常なし |
| `FP Cliff` | Cliff標準Tile Palette | Palette | 0.125 | Tilemap default | Scene依存 | 壁/崖 | 可 | ファイル上の異常なし |
| `FP Hills` | Hill標準Tile Palette | Palette | 0.125 | Tilemap default | Scene依存 | 壁/地形 | 可 | ファイル上の異常なし |
| `FP Lake` | Lake標準Tile Palette | Palette | 0.125 | Tilemap default | Scene依存 | 床/水域 | 可 | ファイル上の異常なし |
| `FP Lake Animated` | Lake Animated Tile Palette | Palette | 0.125 | Tilemap default | Scene依存 | 床/水域 | 可 | ファイル上の異常なし |
| `FP River Animated` | River Animated Tile Palette | Palette | 0.125 | Tilemap default | Scene依存 | 床/水域 | 可 | ファイル上の異常なし |
| `FP Waterfall Animated` | Waterfall Animated Tile Palette | Palette | 0.125 | Tilemap default | Scene依存 | 装飾/水域 | 可 | ファイル上の異常なし |
| `FP Rule Tiles` | Rule Tile Palette | Palette | 0.125 | Tilemap default | Scene依存 | 床/壁 | 可 | ファイル上の異常なし |

注: BoundsはPrefab YAML上のSpriteRenderer `m_Size` から読める範囲で記録。Unity EditorのAssetDatabase実測は、エディタが起動中でバッチ検証できないため未実施です。

## Demo Scene / Sample Scene

| Scene | 完成度 | 内容 |
| --- | --- | --- |
| `Scenes/Demo - Forgotten Plains (Rule + Animated Tiles).unity` | 高 | Rule Tile、Animated Tile、Props、Colliderを含む一番広いDemo |
| `Scenes/Demo - Forgotten Plains.unity` | 中 | 標準TileとPropsのDemo |
| `Scenes/Demo - Forgotten Plains River (Animated Tiles).unity` | 中 | River / WaterfallのAnimated Tile確認用 |

最も完成度が高いSceneは `Demo - Forgotten Plains (Rule + Animated Tiles).unity` と判断しました。理由は、3つのDemoの中でRule Tile、Animated Tile、装飾Prefab、Tilemap Colliderがそろっているためです。

## 最良Demo Scene解析

対象:

`Assets/KrishnaPalacio/MINIFANTASY - Forgotten Plains/Scenes/Demo - Forgotten Plains (Rule + Animated Tiles).unity`

### 使用Prefab

| Prefab | 配置数 | 配置範囲 |
| --- | ---: | --- |
| `Flower` | 23 | x -14.94..12.81, y -7.38..17.63 |
| `Creeper` | 19 | x -14.81..10.81, y -5.88..18.88 |
| `Pillar` | 14 | x -14.81..11.88, y -1.44..19.19 |
| `Cattail` | 13 | x -8.13..5.50, y -8.75..-3.63 |
| `Tree` | 13 | x -9.31..14.31, y -8.19..19.56 |
| `GrassLong` | 10 | x -11.19..10.94, y -0.63..18.88 |

### 使用Tilemap

| Tilemap | 分類 | Collider |
| --- | --- | --- |
| `Ground` | 床 | TilemapCollider2D + CompositeCollider2D |
| `GroundDecoration` | 床装飾 | なし |
| `GroundShadow` | 床影 | なし |
| `Water` | 水域 | TilemapCollider2D + CompositeCollider2D |
| `Walls - Vertical` | 壁 | TilemapCollider2D + CompositeCollider2D |
| `Walls - Horizontal` | 壁 | TilemapCollider2D + CompositeCollider2D |
| `Walls - Cliffs (1)` | 崖/壁 | TilemapCollider2D + CompositeCollider2D |
| `Walls - Cliffs (2)` | 崖/壁 | TilemapCollider2D + CompositeCollider2D |
| `Walls - Cliffs (3)` | 崖/壁 | TilemapCollider2D + CompositeCollider2D |
| `Walls - Above Cliffs` | 壁上面/前景 | TilemapCollider2D + CompositeCollider2D |
| `WallsShadow` | 壁影 | なし |

### 要求カテゴリとの対応

| 要求カテゴリ | Demo内の該当 | 判定 |
| --- | --- | --- |
| 床 | `Ground`, `GroundDecoration`, `GroundShadow` | あり |
| 壁 | `Walls - Vertical`, `Walls - Horizontal`, `Walls - Cliffs*` | あり |
| 廊下 | Dirt path / Cobblestone相当の地面表現のみ | 宇宙船廊下としてはなし |
| ドア | なし | 未対応 |
| 照明 | Light / Light2Dなし | 未対応 |
| 大型設備Prefab | `Tree`, `Pillar`のみ | SF設備としてはなし |

## テストScene

作成済み:

`Assets/Game/Maps/ForgottenPlainsMovementTest.unity`

これは最良Demo Sceneを複製した移動確認用Sceneです。原本の `Assets/KrishnaPalacio` 以下は変更していません。

追加スクリプト:

`Assets/Scripts/ImportedEnvironmentTestPlayerDriver.cs`

挙動:

- `ForgottenPlainsMovementTest` をPlayすると、既存 `Assets/Prefabs/PlayerPrefab.prefab` を自動生成
- `Player` と `NetworkObject` はテスト中だけ無効化し、既存ビジュアルを使う
- WASD / 矢印キーで移動
- Ground系Colliderはテスト時だけ無効化し、壁/水域/崖のColliderで移動制限を確認
- CameraはOrthographic Size 6でプレイヤーを追従

## 結論

この素材はトップダウン2Dの描画・Tilemap構成・Collider付き地形の参考には使えます。ただし、ユーザーが求めているSF宇宙船内部マップの完成素材としては不足しています。特にドア、屋内廊下、照明、宇宙船設備、部屋Prefabが存在しないため、宇宙船マップへ使う場合は別のSF環境素材を追加するか、この素材をあくまでTilemap構造サンプルとして扱うのが安全です。
