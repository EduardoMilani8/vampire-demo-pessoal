using UnityEngine;
using UnityEngine.InputSystem;

namespace HollowTide
{
    /// <summary>
    /// Todas as ações de input do jogo, criadas em código (sem asset .inputactions),
    /// para que o projeto funcione logo após abrir, sem montagem manual.
    /// Sem botão de ataque: só movimento e pausa.
    /// </summary>
    public sealed class GameInput
    {
        readonly InputAction _move;
        readonly InputAction _pause;

        public GameInput()
        {
            _move = new InputAction("Move", InputActionType.Value);
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            _move.AddBinding("<Gamepad>/leftStick", processors: "stickDeadzone");
            _move.AddBinding("<Gamepad>/dpad");

            _pause = new InputAction("Pause", InputActionType.Button);
            _pause.AddBinding("<Keyboard>/escape");
            _pause.AddBinding("<Keyboard>/p");
            _pause.AddBinding("<Gamepad>/start");
        }

        public void Enable()
        {
            _move.Enable();
            _pause.Enable();
        }

        public void Disable()
        {
            _move.Disable();
            _pause.Disable();
        }

        public void Dispose()
        {
            _move.Dispose();
            _pause.Dispose();
        }

        /// <summary>Vetor de movimento com magnitude no máximo 1 (diagonais de teclado já normalizadas).</summary>
        public Vector2 Move => Vector2.ClampMagnitude(_move.ReadValue<Vector2>(), 1f);

        public bool PausePressedThisFrame => _pause.WasPressedThisFrame();
    }
}
