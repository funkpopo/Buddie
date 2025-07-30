import React, { createContext, useContext, useState, useEffect, ReactNode } from 'react';

interface ApiKey {
  id: string;
  name: string;
  key: string;
  baseUrl?: string;
}

interface ApiKeyContextType {
  apiKeys: ApiKey[];
  addApiKey: (apiKey: Omit<ApiKey, 'id'>) => void;
  removeApiKey: (id: string) => void;
  updateApiKey: (id: string, updatedKey: Partial<ApiKey>) => void;
  getApiKey: (id: string) => ApiKey | undefined;
}

const ApiKeyContext = createContext<ApiKeyContextType | undefined>(undefined);

export const ApiKeyProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
  const [apiKeys, setApiKeys] = useState<ApiKey[]>([]);

  useEffect(() => {
    const savedApiKeys = localStorage.getItem('apiKeys');
    if (savedApiKeys) {
      try {
        setApiKeys(JSON.parse(savedApiKeys));
      } catch (e) {
        console.error('Failed to parse saved API keys', e);
      }
    }
  }, []);

  useEffect(() => {
    localStorage.setItem('apiKeys', JSON.stringify(apiKeys));
  }, [apiKeys]);

  const addApiKey = (apiKey: Omit<ApiKey, 'id'>) => {
    const newKey: ApiKey = {
      ...apiKey,
      id: Date.now().toString(),
    };
    setApiKeys(prev => [...prev, newKey]);
  };

  const removeApiKey = (id: string) => {
    setApiKeys(prev => prev.filter(key => key.id !== id));
  };

  const updateApiKey = (id: string, updatedKey: Partial<ApiKey>) => {
    setApiKeys(prev => 
      prev.map(key => 
        key.id === id ? { ...key, ...updatedKey } : key
      )
    );
  };

  const getApiKey = (id: string) => {
    return apiKeys.find(key => key.id === id);
  };

  return (
    <ApiKeyContext.Provider value={{ apiKeys, addApiKey, removeApiKey, updateApiKey, getApiKey }}>
      {children}
    </ApiKeyContext.Provider>
  );
};

export const useApiKeys = (): ApiKeyContextType => {
  const context = useContext(ApiKeyContext);
  if (!context) {
    throw new Error('useApiKeys must be used within an ApiKeyProvider');
  }
  return context;
};