namespace PartnerTransactionBff.Infrastructure;

// Kept as a compatibility marker for the original assessment structure.
// Connection creation is now handled by RabbitMqConnectionProvider so that
// RabbitMQ downtime does not prevent the HTTP API from starting.
