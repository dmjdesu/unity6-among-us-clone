# Character Asset Catalog

調査日: 2026-07-20

## 対象

- 追加キャラクター素材: `Assets/Jovial Games/Simple 2D Cute Characters`
- Demo Scene: `Assets/Jovial Games/Simple 2D Cute Characters/Scene/Demo_Scene.unity`
- 今回PlayerPrefabへ接続した見た目: `Soldier_Character_7`

この素材は3Dモデルではなく、PNGパーツを複数の `SpriteRenderer` で組んだ2DキャラクターPrefabです。Humanoid/Generic Rig、Animator Controller、Animation Clipは含まれていません。

## アセット概要

| 種別 | 件数 | 内容 |
| --- | ---: | --- |
| キャラクターPrefab | 9 | Archer, Cave Man, Clown, Monk, Ninja, Pirate, Soldier, Warrior, Wizard |
| Animator Controller | 0 | 未検出 |
| Animation Clip | 0 | 未検出 |
| Model / Mesh | 0 | `.fbx`, `.obj`, `.dae`, `.blend` は未検出 |
| Material | 0 | 独自 `.mat` は未検出 |
| Texture / Sprite | 49 | キャラパーツPNG、背景、Shadowなど |
| Demo Scene | 1 | `Demo_Scene.unity` |

## 共通仕様

| 項目 | 内容 |
| --- | --- |
| Rigタイプ | なし。2D SpriteRendererパーツ構成 |
| Collider | キャラクターPrefabにはなし |
| Rigidbody / Rigidbody2D | なし |
| 使用Material | URP Default Unlit Sprite Material `guid: 9dfc825aed78fcd4ba02077103263b40` |
| URP互換性 | URP Global Settings / Renderer2DのDefault Sprite Materialを使用。Editor.log上もShaderエラーなし |
| モデルの正面方向 | 2D正面絵。XY平面に表示され、カメラ方向へ正面を向く |
| Sprite設定 | `textureType: Sprite`, `spritePixelsToUnits: 100`, `alphaIsTransparency: 1`, `sRGBTexture: 1` |
| Idle | Clipなし。静止PrefabがIdle相当 |
| Walk | Clipなし。今回 `Player.cs` 側でプロシージャル歩行揺れを付与 |
| Run | なし |
| Death | Clipなし。既存 `Player.cs` の死亡時倒れ表現を継続利用 |

## Prefab一覧

BoundsはPrefab YAML上のSpriteRendererのローカル位置と `m_Size` から算出した概算です。Unity Editorの実測Boundsではありません。

| Prefab名 | SpriteRenderer数 | 実寸概算 | Collider | Rigidbody | Animator | Material | 用途 |
| --- | ---: | --- | --- | --- | --- | --- | --- |
| `Archer_Character_1` | 6 | 1.37 x 1.90 Unit | なし | なし | なし | URP Default Unlit Sprite | 弓兵キャラ |
| `Cave_Man_Character_2` | 6 | 1.66 x 1.90 Unit | なし | なし | なし | URP Default Unlit Sprite | 原始人キャラ |
| `Clown_Character_3` | 6 | 1.40 x 1.55 Unit | なし | なし | なし | URP Default Unlit Sprite | 道化師キャラ |
| `Monk_Character_4` | 6 | 1.49 x 1.38 Unit | なし | なし | なし | URP Default Unlit Sprite | 僧侶キャラ |
| `Ninja_Character_5` | 6 | 1.28 x 1.41 Unit | なし | なし | なし | URP Default Unlit Sprite | 忍者キャラ |
| `Pirate_Character_6` | 6 | 1.53 x 1.84 Unit | なし | なし | なし | URP Default Unlit Sprite | 海賊キャラ |
| `Soldier_Character_7` | 6 | 1.78 x 1.77 Unit | なし | なし | なし | URP Default Unlit Sprite | 今回採用したプレイヤー見た目 |
| `Warrior_Character_8` | 7 | 1.70 x 1.51 Unit | なし | なし | なし | URP Default Unlit Sprite | 戦士キャラ |
| `Wizard_Character_9` | 6 | 1.58 x 1.77 Unit | なし | なし | なし | URP Default Unlit Sprite | 魔法使いキャラ |

## Soldier_Character_7 詳細

| パーツ | Path | Sprite GUID |
| --- | --- | --- |
| Body | `Characters/Soldier_Character_7/Body.png` | `45326424b4d07934984cf41ad858d32b` |
| Head | `Characters/Soldier_Character_7/Head.png` | `814bff80774c25f47a52bb4ced3012ed` |
| Left Foot | `Characters/Soldier_Character_7/Left_Foot.png` | `9bf00a93a102fec40b5a4c42099f56f8` |
| Right Foot | `Characters/Soldier_Character_7/Right_Foot.png` | `6bb65cd73687f7a4ca2143ca3a5f204a` |
| Weapon | `Characters/Soldier_Character_7/Weapon.png` | `ba75aa2474d9c1942a4f502fbf66db9e` |
| Shadow | `Shadow.png` | `87dd2d469b826384c9bd6d4de7aa6b76` |

## PlayerPrefabへの接続方針

既存のPhoton FusionプレイヤーPrefabでは、ルート `PlayerPrefab` が `NetworkObject` と `AmongUsClone.Player` を持っています。今回の差し替えではルートObject、`NetworkObject`、NetworkedBehaviours、Input Authority、State Authorityに関わる設定は変更していません。

実装内容:

- `Player.cs` にキャラクターパーツ用Sprite参照を追加
- `PlayerPrefab.prefab` の `Player` componentにSoldierのSprite参照を設定
- 実行時に旧MeshRendererを非表示にし、Jovial SoldierのSpriteRenderer構成を生成
- 移動、キル、通報、投票、タスク、サボタージュ、ベント処理は既存のまま
- 色変更は `Renderer.material` を使わず、`SpriteRenderer.color` と `MaterialPropertyBlock` で適用
- BodyとFootをプレイヤーカラー化し、Head/Weaponは元色を維持

## アニメーション対応状況

| 種別 | 素材側 | 現在のゲーム側 |
| --- | --- | --- |
| Idle | Clipなし | 静止表示 |
| Walk | Clipなし | 移動量から足、頭、武器、全体のボブをプロシージャル生成 |
| Run | Clipなし | 未実装。Walk速度表現のみ |
| Death | Clipなし | 既存の倒れ/縮み表現を継続 |

## 注意

この素材はファンタジー寄りの2Dキャラクターです。宇宙船・SFテーマへ寄せるには、今後ヘルメット、宇宙服、バックパック、バイザーなどの専用パーツを追加するか、現在のパーツを置き換える必要があります。
