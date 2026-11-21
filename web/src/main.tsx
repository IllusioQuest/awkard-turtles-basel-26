import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router'
import './index.css'
import AdminPage from './admin/AdminPage.tsx'
import HouseholdPage from './household/HouseholdPage.tsx'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<Navigate to="/admin" replace />} />
        <Route path="/admin" element={<AdminPage />} />
        <Route path="/household" element={<HouseholdPage />} />
        <Route path="/household/:siteId" element={<HouseholdPage />} />
      </Routes>
    </BrowserRouter>
  </StrictMode>,
)
