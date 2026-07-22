using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AmongUsClone
{
    [DisallowMultipleComponent]
    public sealed class ImportedEnvironmentTestPlayerDriver : MonoBehaviour
    {
        private const string TestSceneName = "ForgottenPlainsMovementTest";
        private const string PlayerPrefabPath = "Assets/Prefabs/PlayerPrefab.prefab";
        private const float MoveSpeed = 4f;
        private const float PlayerRadius = 0.45f;

        private readonly RaycastHit2D[] _hits = new RaycastHit2D[8];
        private Rigidbody2D _rigidbody;
        private ContactFilter2D _movementFilter;
        private Camera _camera;

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void SpawnEditorMovementPlayer()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.name != TestSceneName)
            {
                return;
            }

            if (Object.FindAnyObjectByType<ImportedEnvironmentTestPlayerDriver>() != null)
            {
                return;
            }

            DisableDemoCameraController();
            DisableWalkableTilemapColliders();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"Movement test could not load existing player prefab at {PlayerPrefabPath}.");
                return;
            }

            var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (instance == null)
            {
                Debug.LogError($"Movement test could not instantiate {PlayerPrefabPath}.");
                return;
            }

            instance.name = "Movement Test Player";
            instance.transform.position = new Vector3(0f, 0f, 0f);

            if (instance.TryGetComponent(out Player player))
            {
                player.enabled = false;
            }

            if (instance.TryGetComponent(out NetworkObject networkObject))
            {
                networkObject.enabled = false;
            }

            instance.AddComponent<ImportedEnvironmentTestPlayerDriver>();
        }

        private static void DisableDemoCameraController()
        {
            foreach (var behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (behaviour != null && behaviour.GetType().FullName == "Minifantasy.CameraController")
                {
                    behaviour.enabled = false;
                }
            }
        }

        private static void DisableWalkableTilemapColliders()
        {
            foreach (var collider in Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
            {
                var objectName = collider.gameObject.name;
                if (objectName == "Ground" || objectName == "GroundDecoration" || objectName == "GroundShadow")
                {
                    collider.enabled = false;
                }
            }
        }
#endif

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
            if (_rigidbody == null)
            {
                _rigidbody = gameObject.AddComponent<Rigidbody2D>();
            }

            _rigidbody.bodyType = RigidbodyType2D.Kinematic;
            _rigidbody.gravityScale = 0f;
            _rigidbody.freezeRotation = true;
            _rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var circle = GetComponent<CircleCollider2D>();
            if (circle == null)
            {
                circle = gameObject.AddComponent<CircleCollider2D>();
            }

            circle.radius = PlayerRadius;
            circle.isTrigger = false;

            _movementFilter = new ContactFilter2D
            {
                useTriggers = false,
            };
            _movementFilter.SetLayerMask(Physics2D.DefaultRaycastLayers);

            _camera = Camera.main;
            if (_camera != null)
            {
                _camera.orthographic = true;
                _camera.orthographicSize = 6f;
            }
        }

        private void FixedUpdate()
        {
            var input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (input.sqrMagnitude > 1f)
            {
                input.Normalize();
            }

            var motion = input * (MoveSpeed * Time.fixedDeltaTime);
            if (motion.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            MoveWithCollision(motion);
        }

        private void LateUpdate()
        {
            if (_camera == null)
            {
                return;
            }

            var position = transform.position;
            _camera.transform.position = new Vector3(position.x, position.y, _camera.transform.position.z);
        }

        private void MoveWithCollision(Vector2 motion)
        {
            if (CanMove(motion))
            {
                _rigidbody.MovePosition(_rigidbody.position + motion);
                return;
            }

            var hitNormal = _hits[0].normal;
            var slideMotion = motion - hitNormal * Vector2.Dot(motion, hitNormal);
            if (slideMotion.sqrMagnitude > 0.000001f && CanMove(slideMotion))
            {
                _rigidbody.MovePosition(_rigidbody.position + slideMotion);
            }
        }

        private bool CanMove(Vector2 motion)
        {
            var distance = motion.magnitude;
            if (distance <= 0f)
            {
                return true;
            }

            var hitCount = _rigidbody.Cast(motion.normalized, _movementFilter, _hits, distance + 0.02f);
            return hitCount == 0;
        }
    }
}
