CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `ProductVersion` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK___EFMigrationsHistory` PRIMARY KEY (`MigrationId`)
) CHARACTER SET=utf8mb4;

START TRANSACTION;
ALTER DATABASE CHARACTER SET utf8mb4;

CREATE TABLE `category` (
    `category_id` int NOT NULL AUTO_INCREMENT,
    `category_name` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_category` PRIMARY KEY (`category_id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `user` (
    `user_id` int NOT NULL AUTO_INCREMENT,
    `username` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `user_fname` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `user_lname` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `password` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `phone_number` varchar(20) CHARACTER SET utf8mb4 NULL,
    `address` varchar(100) CHARACTER SET utf8mb4 NULL,
    `created_at` datetime(6) NOT NULL,
    `user_type` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_user` PRIMARY KEY (`user_id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `menu_item` (
    `menu_item_id` int NOT NULL AUTO_INCREMENT,
    `item_name` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `image` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `category_id` int NOT NULL,
    `user_id` int NOT NULL,
    `price` decimal(10,2) NOT NULL,
    `is_enabled` tinyint(1) NOT NULL,
    `created_at` datetime(6) NOT NULL,
    CONSTRAINT `PK_menu_item` PRIMARY KEY (`menu_item_id`),
    CONSTRAINT `FK_menu_item_category_category_id` FOREIGN KEY (`category_id`) REFERENCES `category` (`category_id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_menu_item_user_user_id` FOREIGN KEY (`user_id`) REFERENCES `user` (`user_id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

CREATE UNIQUE INDEX `IX_category_category_name` ON `category` (`category_name`);

CREATE INDEX `IX_menu_item_category_id` ON `menu_item` (`category_id`);

CREATE INDEX `IX_menu_item_user_id` ON `menu_item` (`user_id`);

CREATE UNIQUE INDEX `IX_user_username` ON `user` (`username`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20251004160436_AddMenuTables', '9.0.9');

COMMIT;

