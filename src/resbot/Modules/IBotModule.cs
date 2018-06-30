namespace resbot.Modules
{
    /// <summary>
    /// Module definition
    /// </summary>
    interface IBotModule
    {
        //Invoked on bot startup
        void Startup();

        // Invokedon bot shutdown
        void Shutdown();
    }
}
