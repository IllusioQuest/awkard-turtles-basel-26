import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter, Route, Routes } from 'react-router'
import './index.css'
import App from './App.tsx'
import AdminPage from './admin/AdminPage.tsx'
import HouseholdPage from './household/HouseholdPage.tsx'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<App />} />
        <Route path="/admin" element={<AdminPage />} />
        <Route path="/household" element={<HouseholdPage />} />
        <Route path="/household/:siteId" element={<HouseholdPage />} />
      </Routes>
    </BrowserRouter>
  </StrictMode>,
)
