-- 0003：示範帳號（Plan.md §10.2）。
-- Repo 是公開的，這裡的密碼是公開的示範值，不得用於真實帳號；轉入正式營運前以
-- db/cleanup/remove_demo_data.sql 刪除。
--   demo_customer／Demo@1234：一般會員（uid 1001，位於 1～4998）
--   demo_seller／Demo@1234：賣家（uid 5001，> 5000 視為賣家）
INSERT INTO dbo.violet_user_login (
    uname, email, username, password, phone, dob, country, state, city,
    gender, address, secq, seca, uid
)
SELECT
    v.uname,
    v.email,
    v.username,
    v.password,
    v.phone,
    v.dob,
    v.country,
    v.state,
    v.city,
    v.gender,
    v.address,
    v.secq,
    v.seca,
    v.uid
FROM (
    VALUES
    (
        'Demo Customer', 'demo_customer@example.com', 'demo_customer',
        'Demo@1234', 9000000001, '1995-05-05', 'Taiwan', 'Taipei', 'Taipei',
        'Female', 'Demo customer address',
        'What is the name of your first school?', 'demo', 1001
    ),
    (
        'Demo Seller', 'demo_seller@example.com', 'demo_seller',
        'Demo@1234', 9000000002, '1990-01-01', 'Taiwan', 'Taipei', 'Taipei',
        'Male', 'Demo seller address',
        'What is the name of your first school?', 'demo', 5001
    )
)
    AS v (
        uname, email, username, password, phone, dob, country, state, city,
        gender, address, secq, seca, uid
    )
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.violet_user_login AS u
    WHERE
        u.uname = v.uname
        OR u.username = v.username
        OR u.email = v.email
        OR u.phone = v.phone
);
