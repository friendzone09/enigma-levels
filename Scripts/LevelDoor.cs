using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Puerta/portal de transición entre niveles.
/// Al entrar en su trigger carga la escena indicada.
/// </summary>
public class LevelDoor : MonoBehaviour
{
    [Tooltip("Nombre exacto de la escena de destino (debe estar en Build Settings).")]
    public string targetScene;

    [Tooltip("Tiempo mínimo desde que carga la escena antes de permitir el viaje.")]
    public float activationDelay = 0.75f;

    private bool loading;

    private void OnTriggerEnter2D(Collider2D other) => TryLoad(other);

    // También comprobamos "Stay" por si el jugador ya está dentro del trigger
    // cuando termina el tiempo de activación inicial.
    private void OnTriggerStay2D(Collider2D other) => TryLoad(other);

    private void TryLoad(Collider2D other)
    {
        if (loading || string.IsNullOrEmpty(targetScene))
            return;

        if (Time.timeSinceLevelLoad < activationDelay)
            return;

        if (other.GetComponentInParent<PlayerController2D>() == null)
            return;

        loading = true;
        SceneManager.LoadScene(targetScene);
    }
}
