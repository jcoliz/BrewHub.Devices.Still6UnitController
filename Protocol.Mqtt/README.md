# BrewHub.Protocol.Mqtt

This small project encapsulates the logic for how BrewHeb composes MQTT messages.
It can be shared across all projects which produce or consume MQTT messages.

Question: Should this project encapsulate the CLIENT? This might be easier.

For now, I want to have a central place to change TOPIC generation, and MESSAGE
composition. So those are all I want to return.