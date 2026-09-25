using UnityEngine;
using UnityEngine.InputSystem;

namespace AiLab01
{
    /// <summary>
    /// Кодовый аналог сгенерированного класса действий Input System.
    /// Ассет InputSystem_Actions в проекте есть, но без биндингов, поэтому карта
    /// «Player» собирается прямо в коде: Move (WASD + стрелки) и Attack
    /// (Пробел / Enter / левая кнопка мыши). Использование — как у сгенерированного
    /// класса: new PlayerInput() → Enable()/Disable() → Player.Attack.performed.
    /// </summary>
    public class PlayerInput : System.IDisposable
    {
        /// Карта «Player»: ссылки на готовые действия.
        public class PlayerActions
        {
            public InputAction Move;    // Vector2
            public InputAction Attack;  // Button

            internal PlayerActions(InputActionMap map)
            {
                Move = map.FindAction("Move", throwIfNotFound: true);
                Attack = map.FindAction("Attack", throwIfNotFound: true);
            }
        }

        public PlayerActions Player { get; }

        readonly InputActionAsset asset;

        public PlayerInput()
        {
            asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "PlayerInput";

            var map = new InputActionMap("Player");

            var move = map.AddAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");

            var attack = map.AddAction("Attack", InputActionType.Button);
            attack.AddBinding("<Keyboard>/space");
            attack.AddBinding("<Keyboard>/enter");
            attack.AddBinding("<Mouse>/leftButton");

            asset.AddActionMap(map);
            Player = new PlayerActions(map);
        }

        public void Enable() => asset.Enable();

        public void Disable() => asset.Disable();

        public void Dispose()
        {
            asset.Disable();
            Object.Destroy(asset);
        }
    }
}
