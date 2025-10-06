# Pizzafy Web Application

A pizza delivery web application built with ASP.NET Core 8 and MySQL.

## Features

- User authentication (login/logout/register)
- Role-based access (Customer/Admin)
- Responsive design with pizza-themed UI
- MySQL database integration with Entity Framework Core

## Database Setup

1. **Create MySQL Database:**
   ```sql
   CREATE DATABASE pizzafy_db;
   ```

2. **Create User Table:**
   ```sql
   USE pizzafy_db;
   
   CREATE TABLE user (
       user_id INT AUTO_INCREMENT PRIMARY KEY,
       username VARCHAR(50) NOT NULL UNIQUE,   
       user_fname VARCHAR(30) NOT NULL,
       user_lname VARCHAR(30) NOT NULL,
       password VARCHAR(50) NOT NULL,
       phone_number VARCHAR(20),
       address VARCHAR(100),
       created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
       user_type ENUM('customer','admin') DEFAULT 'customer'
   );
   ```

3. **Update Connection String:**
   Update the connection string in `appsettings.json` and `appsettings.Development.json`:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=localhost;Database=pizzafy_db;Uid=your_username;Pwd=your_password;"
     }
   }
   ```

## Running the Application

1. **Install Dependencies:**
   ```bash
   dotnet restore
   ```

2. **Run the Application:**
   ```bash
   dotnet run --project PizzafyWeb/PizzafyWeb.csproj --launch-profile https
   ```

3. **Access the Application:**
   - Navigate to `https://localhost:7xxx/Login` for the login page
   - The application will automatically seed the database with default users

## Default Users

The application creates two default users for testing:

### Admin User
- **Username:** admin
- **Password:** admin123
- **Type:** Administrator

### Customer User
- **Username:** customer
- **Password:** customer123
- **Type:** Customer

## Project Structure

```
PizzafyWeb/
??? Data/
?   ??? PizzafyDbContext.cs          # Entity Framework DbContext
??? Models/
?   ??? User.cs                      # User entity model
?   ??? LoginModel.cs                # Login form model
?   ??? RegisterModel.cs             # Registration form model
??? Pages/
?   ??? Login.cshtml/.cs             # Login page
?   ??? Register.cshtml/.cs          # Registration page
?   ??? Logout.cshtml.cs             # Logout handler
?   ??? Index.cshtml                 # Home page
??? Services/
?   ??? DatabaseSeeder.cs            # Database seeding service
??? Program.cs                       # Application configuration
```

## Technologies Used

- **ASP.NET Core 8** - Web framework
- **Entity Framework Core** - ORM
- **MySQL** - Database
- **Pomelo.EntityFrameworkCore.MySql** - MySQL provider
- **Cookie Authentication** - User authentication
- **Bootstrap** - UI framework

## Security Notes

- Passwords are hashed using MD5 (for demonstration purposes)
- In production, use stronger hashing algorithms like bcrypt or Argon2
- Update default passwords before deploying to production
- Use secure connection strings and environment variables

## License

This project is for educational purposes.