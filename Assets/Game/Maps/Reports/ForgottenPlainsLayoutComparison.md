# Forgotten Plains Layout Comparison

MINIFANTASY - Forgotten Plainsの既存Sprite、Tile、Tilemap素材だけを使用する前提で3案を自動生成・評価した結果です。

## Asset Classification

- 歩行可能な地面/草地: 19 tile assets
- 道/土/石畳: 109 tile assets
- 水/湖: 90 tile assets
- 川/アニメーションタイル: 67 river tiles, 30 lake animated tiles
- 崖: 85 tile assets
- 壁/遺跡: 22 tile assets
- 木: Assets/KrishnaPalacio/MINIFANTASY - Forgotten Plains/Prefabs/Props/Tree.prefab
- 柱/遺跡装飾: Assets/KrishnaPalacio/MINIFANTASY - Forgotten Plains/Prefabs/Props/Pillar.prefab
- 大型/小型装飾: GrassLong, Flower, Creeper, Cattail prefab
- 橋/建物/キャンプ設備: 専用Prefabは見つからないため、既存の土/石畳/壁/柱/木素材で渡り道、建物風外形、キャンプ設備を構成

## Layout Metrics

| Layout | Type | Size | Areas | Connections | Cycles | Dead Ends | Longest Distance | Spawn Avg | Chokepoints | Walkable | Blocked | Score |
|---|---|---:|---:|---:|---:|---:|---:|---:|---|---:|---:|---:|
| A | 中央集約型 | 170x124 | 9 | 12 | 4 | 0 | 174.0 | 61.6 |  | 10999 | 3646 | 60.0 |
| B | 外周回遊型 | 170x124 | 9 | 12 | 4 | 0 | 188.7 | 68.0 |  | 9566 | 3628 | 59.5 |
| C | 複数ハブ型 | 170x124 | 9 | 15 | 7 | 0 | 179.3 | 63.0 |  | 11059 | 3636 | 117.9 |

## Layout Notes

### Layout A: Layout A: Centralized Hub

- 特徴: 中央集約型
- 見通しの良い領域: Central Village, Riverside Crossing, Southern Lake outer path
- 視界が遮られる領域: Northern Forest, Eastern Ruins, Ancient Sanctuary, Abandoned Outpost
- タスク分布: Central Village:2, Northern Forest:2, Eastern Ruins:2, Southern Lake:2, Western Camp:2, Ancient Sanctuary:2, Rocky Pass:2, Riverside Crossing:2, Abandoned Outpost:2
- チョークポイント候補: なし

### Layout B: Layout B: Outer Ring

- 特徴: 外周回遊型
- 見通しの良い領域: Central Village, Riverside Crossing, Southern Lake outer path
- 視界が遮られる領域: Northern Forest, Eastern Ruins, Ancient Sanctuary, Abandoned Outpost
- タスク分布: Central Village:2, Northern Forest:2, Eastern Ruins:2, Southern Lake:2, Western Camp:2, Ancient Sanctuary:2, Rocky Pass:2, Riverside Crossing:2, Abandoned Outpost:2
- チョークポイント候補: なし

### Layout C: Layout C: Multiple Hubs

- 特徴: 複数ハブ型
- 見通しの良い領域: Central Village, Riverside Crossing, Southern Lake outer path
- 視界が遮られる領域: Northern Forest, Eastern Ruins, Ancient Sanctuary, Abandoned Outpost
- タスク分布: Central Village:2, Northern Forest:2, Eastern Ruins:2, Southern Lake:2, Western Camp:2, Ancient Sanctuary:2, Rocky Pass:2, Riverside Crossing:2, Abandoned Outpost:2
- チョークポイント候補: なし

## Selected Layout

採用案: Layout C (Layout C: Multiple Hubs)

採用理由: 複数ハブ型で中央集落に依存しすぎず、東西・南北とも迂回経路を持ち、15人でも狭すぎず4〜8人でもRiverside Crossing/Central Village/Eastern Ruinsで遭遇が起きやすい構造です。Kill後の逃走経路が複数あり、TaskPointも各エリア2個ずつで偏りません。Colliderは矩形ベースの水・崖・壁・大型Propsに限定できるため安定して構築できます。
