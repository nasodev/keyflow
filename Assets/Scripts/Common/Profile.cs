namespace KeyFlow
{
    public enum Profile { Nayoon, Soyoon }

    public static class SessionProfile
    {
        public static Profile Current { get; set; } = Profile.Nayoon;
    }
}
