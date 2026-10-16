CREATE INDEX IF NOT EXISTS `ix_friends_requests_target_user_id`
ON `friends_requests` (`target_user_id`)
WAIT 5
ALGORITHM=NOCOPY
LOCK=NONE;

CREATE INDEX IF NOT EXISTS `ix_friends_user_id_2`
ON `friends` (`user_id_2`)
WAIT 5
ALGORITHM=NOCOPY
LOCK=NONE;

CREATE INDEX IF NOT EXISTS `ix_users_banned`
ON `users` (`banned`)
WAIT 5
ALGORITHM=NOCOPY
LOCK=NONE;
