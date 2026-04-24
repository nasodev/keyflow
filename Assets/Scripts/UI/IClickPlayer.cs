namespace KeyFlow.UI
{
    // Thin audio abstraction for CountdownOverlay. Production impl wraps
    // AudioSource.PlayOneShot + pitch; tests inject a spy that records calls.
    public interface IClickPlayer
    {
        void Play(float pitch);
    }
}
