#!/bin/bash
set -e

mongosh -u "$MONGO_INITDB_ROOT_USERNAME" -p "$MONGO_INITDB_ROOT_PASSWORD" admin --eval "
  db = db.getSiblingDB('$DB_MONGO_DROPS_NAME');
  db.createUser({
    user: '$DB_MONGO_USER',
    pwd: '$DB_MONGO_PASSWORD',
    roles: [
      { role: 'readWrite', db: '$DB_MONGO_DROPS_NAME' },
      { role: 'readWrite', db: '$DB_MONGO_SOCIAL_NAME' }
    ]
  });
"